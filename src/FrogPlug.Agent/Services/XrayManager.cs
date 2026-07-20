using FrogPlug.Core;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FrogPlug.Agent.Services;

public sealed class XrayManager
{
    private readonly AgentOptions _options;
    private readonly ICommandRunner _runner;
    private readonly IFileSystem _files;
    private readonly IServiceController _services;

    public XrayManager(IOptions<AgentOptions> options, ICommandRunner runner, IFileSystem files)
        : this(options, runner, files, new SystemdServiceController(runner))
    {
    }

    public XrayManager(
        IOptions<AgentOptions> options,
        ICommandRunner runner,
        IFileSystem files,
        IServiceController services)
    {
        _options = options.Value;
        _runner = runner;
        _files = files;
        _services = services;
    }

    // Добавление и удаление пользователей без перезапуска.
    public async Task<(bool Success, string Error)> ApplyAsync(string config, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.XrayConfigPath) ?? ".";
        _files.CreateDirectory(directory);
        if (await FileContent.EqualsNormalizedAsync(_files, _options.XrayConfigPath, config, cancellationToken))
        {
            return (true, string.Empty);
        }

        if (await TryApplyClientDiffAsync(config, cancellationToken))
        {
            return (true, string.Empty);
        }

        // Xray определяет формат по расширению, поэтому временный файл должен оканчиваться на .json.
        var temporary = TemporaryConfigPath(_options.XrayConfigPath);
        var backup = _options.XrayConfigPath + ".bak";
        await _files.WriteTextAsync(temporary, config, cancellationToken);
        var validation = await _runner.RunAsync(_options.XrayBinary,
            ["run", "-test", "-c", temporary], TimeSpan.FromSeconds(20), cancellationToken);
        if (validation.ExitCode != 0)
        {
            _files.DeleteFile(temporary);
            return (false, LimitError(validation.StandardOutput + validation.StandardError));
        }
        if (_files.FileExists(_options.XrayConfigPath))
        {
            _files.CopyFile(_options.XrayConfigPath, backup, true);
        }
        _files.MoveFile(temporary, _options.XrayConfigPath, true);
        var restart = await RestartAsync(cancellationToken);
        if (restart.ExitCode == 0)
        {
            return (true, string.Empty);
        }
        if (_files.FileExists(backup))
        {
            _files.CopyFile(backup, _options.XrayConfigPath, true);
            await RestartAsync(cancellationToken);
        }
        return (false, LimitError(restart.StandardOutput + restart.StandardError));
    }

    private async Task<bool> TryApplyClientDiffAsync(string config, CancellationToken cancellationToken)
    {
        if (!_files.FileExists(_options.XrayConfigPath))
        {
            return false;
        }

        var current = await _files.ReadTextAsync(_options.XrayConfigPath, cancellationToken);
        var diff = XrayClientDiff.Compare(current, config);
        if (!diff.IsClientOnlyChange || diff.Changes.Count == 0)
        {
            return false;
        }

        var additionsPath = _options.XrayConfigPath + ".users.json";
        try
        {
            foreach (var change in diff.Changes.Where(item => item.AddedClients.Count > 0))
            {
                var additions = BuildAdditionsConfig(change, config);
                await _files.WriteTextAsync(additionsPath, additions, cancellationToken);
                var result = await _runner.RunAsync(
                    _options.XrayBinary,
                    ["api", "adu", "--server=127.0.0.1:10085", additionsPath],
                    TimeSpan.FromSeconds(20),
                    cancellationToken);
                // xray api adu возвращает 0 и при ошибке добавления.
                if (result.ExitCode != 0 || ReportedCount(result.StandardOutput, "Added") != change.AddedClients.Count)
                {
                    return false;
                }
            }

            foreach (var change in diff.Changes.Where(item => item.RemovedEmails.Count > 0))
            {
                var arguments = new List<string>
                {
                    "api",
                    "rmu",
                    "--server=127.0.0.1:10085",
                    $"-tag={change.Tag}"
                };
                arguments.AddRange(change.RemovedEmails);
                var result = await _runner.RunAsync(
                    _options.XrayBinary,
                    arguments,
                    TimeSpan.FromSeconds(20),
                    cancellationToken);
                if (result.ExitCode != 0 || ReportedCount(result.StandardOutput, "Removed") != change.RemovedEmails.Count)
                {
                    return false;
                }
            }

            await _files.WriteTextAsync(_options.XrayConfigPath, config, cancellationToken);
            return true;
        }
        finally
        {
            _files.DeleteFile(additionsPath);
        }
    }

    // adu собирает вход заново, поэтому нужны listen, port и protocol (иначе «Added 0 user(s)» при коде 0).
    public static string BuildAdditionsConfig(FrogPlug.Core.XrayClientChange change, string fullConfig)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(fullConfig)!;
        var inbound = root["inbounds"]!.AsArray()
            .First(item => (string?)item!["tag"] == change.Tag)!;
        var clients = new System.Text.Json.Nodes.JsonArray(
            change.AddedClients.Select(client => (System.Text.Json.Nodes.JsonNode)client.DeepClone()).ToArray());
        var addition = new System.Text.Json.Nodes.JsonObject
        {
            ["tag"] = change.Tag,
            ["listen"] = inbound["listen"]?.DeepClone(),
            ["port"] = inbound["port"]?.DeepClone(),
            ["protocol"] = inbound["protocol"]?.DeepClone(),
            ["settings"] = new System.Text.Json.Nodes.JsonObject
            {
                ["clients"] = clients,
                ["decryption"] = "none"
            }
        };
        var document = new System.Text.Json.Nodes.JsonObject
        {
            ["inbounds"] = new System.Text.Json.Nodes.JsonArray(addition)
        };
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    // Строка вида «Added 1 user(s) in total.» / «Removed 1 user(s) in total.».
    public static int ReportedCount(string output, string verb)
    {
        var match = System.Text.RegularExpressions.Regex.Match(output ?? string.Empty, verb + @"\s+(\d+)\s+user");
        return match.Success ? int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
    }

    public Task<CommandResult> RestartAsync(CancellationToken cancellationToken)
    {
        return _services.RestartAsync("xray", cancellationToken);
    }

    public async Task<TrafficStats> GetStatsAsync(bool reset, CancellationToken cancellationToken)
    {
        var arguments = new List<string> { "api", "statsquery", "--server=127.0.0.1:10085", "-pattern", "user>>>" };
        if (reset)
        {
            arguments.Add("-reset");
        }
        var result = await _runner.RunAsync(_options.XrayBinary, arguments, TimeSpan.FromSeconds(20), cancellationToken);
        return result.ExitCode == 0 ? AgentParsers.ParseStats(result.StandardOutput) : new TrafficStats(new Dictionary<string, TrafficStat>());
    }

    private static string LimitError(string error) => error.Length <= 4096 ? error : error[..4096];

    public static string TemporaryConfigPath(string configPath)
    {
        var directory = Path.GetDirectoryName(configPath) ?? string.Empty;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(configPath) + ".new.json");
    }
}
