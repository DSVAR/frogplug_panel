using Microsoft.Extensions.Options;

namespace FrogPlug.Agent.Services;

public sealed class CaddyManager
{
    private readonly AgentOptions _options;
    private readonly ICommandRunner _runner;
    private readonly IFileSystem _files;
    private readonly IServiceController _services;

    public CaddyManager(IOptions<AgentOptions> options, ICommandRunner runner, IFileSystem files)
        : this(options, runner, files, new SystemdServiceController(runner))
    {
    }

    public CaddyManager(
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

    public async Task<(bool Success, string Error)> ApplyAsync(FrogPlug.Core.CaddyApplyRequest request, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.CaddyfilePath) ?? ".";
        _files.CreateDirectory(directory);
        var content = FrogPlug.Core.CaddyfileBuilder.Build(
            request.Domain,
            request.XhttpPath,
            request.XhttpUpstreamPort,
            request.XhttpEnabled,
            request.MtprotoHost,
            string.IsNullOrWhiteSpace(_options.PanelUpstream) ? null : _options.PanelUpstream,
            _options.PanelPath,
            _options.SubscriptionPath,
            request.TgWebEnabled,
            request.DocsHost);
        if (await FileContent.EqualsNormalizedAsync(_files, _options.CaddyfilePath, content, cancellationToken))
        {
            return (true, string.Empty);
        }

        // Рабочий файл не трогаем, пока новый не прошёл проверку; при сбое перезапуска возвращаем старый.
        var temporary = _options.CaddyfilePath + ".new";
        var backup = _options.CaddyfilePath + ".bak";
        await _files.WriteTextAsync(temporary, content, cancellationToken);
        var validation = await _runner.RunAsync(
            "caddy",
            ["validate", "--config", temporary, "--adapter", "caddyfile"],
            TimeSpan.FromSeconds(20),
            cancellationToken);
        if (validation.ExitCode != 0)
        {
            _files.DeleteFile(temporary);
            return (false, Describe(validation));
        }

        var hadPrevious = _files.FileExists(_options.CaddyfilePath);
        if (hadPrevious)
        {
            _files.CopyFile(_options.CaddyfilePath, backup, true);
        }

        _files.MoveFile(temporary, _options.CaddyfilePath, true);
        var reload = await ReloadAsync(cancellationToken);
        if (reload.ExitCode == 0)
        {
            return (true, string.Empty);
        }

        if (hadPrevious)
        {
            _files.CopyFile(backup, _options.CaddyfilePath, true);
            await ReloadAsync(cancellationToken);
        }

        return (false, Describe(reload));
    }

    private Task<CommandResult> ReloadAsync(CancellationToken cancellationToken)
    {
        if (string.Equals(_options.ServiceMode, "docker", StringComparison.OrdinalIgnoreCase))
        {
            return _runner.RunAsync(
                "caddy",
                ["reload", "--config", _options.CaddyfilePath, "--adapter", "caddyfile"],
                TimeSpan.FromSeconds(20),
                cancellationToken);
        }

        return _services.ReloadAsync("caddy", cancellationToken);
    }

    // Caddy и supervisorctl пишут причину то в stderr, то в stdout — берём обе.
    private static string Describe(CommandResult result)
    {
        var text = string.Join('\n', new[] { result.StandardError, result.StandardOutput }
            .Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        if (text.Length == 0)
        {
            text = $"Код завершения {result.ExitCode}";
        }

        return text.Length <= 4096 ? text : text[^4096..];
    }
}
