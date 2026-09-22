using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FrogPlug.Core;
using Microsoft.Extensions.Options;

namespace FrogPlug.Agent.Services;

public sealed record OpenFluxResult(bool Success, string? Error, string Output);

public sealed class OpenFluxManager(
    IFileSystem files,
    ICommandRunner runner,
    HttpClient http,
    IServiceController services,
    IOptions<AgentOptions> options)
{
    private const string InstallerPath = "/opt/openflux-node/node-install.sh";
    private const string ConfigRoot = "/etc/openflux-node";
    private const string SupervisorRoot = "/etc/supervisor/conf.d";

    public OpenFluxManager(IFileSystem files, ICommandRunner runner, HttpClient http)
        : this(files, runner, http, new SystemdServiceController(runner), Options.Create(new AgentOptions()))
    {
    }

    public OpenFluxManager(
        IFileSystem files,
        ICommandRunner runner,
        HttpClient http,
        IServiceController services)
        : this(files, runner, http, services, Options.Create(new AgentOptions()))
    {
    }

    // Каналы белых списков.
    public async Task<OpenFluxResult> ApplyAsync(OpenFluxApplyRequest request, CancellationToken cancellationToken)
    {
        if (IsDockerMode())
        {
            return await ApplyDockerAsync(request, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.InstallerSha256))
        {
            return Failed("Не задан SHA-256 установщика OpenFlux.");
        }

        if (!OpenFluxChannelValidator.IsChannel(request.Channel))
        {
            return Failed("Недопустимое имя канала OpenFlux.");
        }

        try
        {
            var installer = await DownloadInstaller(request, cancellationToken);
            var verified = VerifyInstaller(installer, request.InstallerSha256);
            if (!verified)
            {
                return Failed("SHA-256 установщика OpenFlux не совпадает.");
            }

            files.CreateDirectory("/opt/openflux-node");
            await files.WriteTextAsync(InstallerPath, installer, cancellationToken);
            var configPath = files.GetTempFileName();
            try
            {
                await files.WriteTextAsync(configPath, BuildConfig(request), cancellationToken);
                await runner.RunAsync("chmod", ["0600", configPath], TimeSpan.FromSeconds(10), cancellationToken);
                var result = await runner.RunAsync("bash", [InstallerPath, "apply", configPath], TimeSpan.FromMinutes(3), cancellationToken);
                if (result.ExitCode != 0)
                {
                    return Failed("Установщик OpenFlux завершился с ошибкой.", result.StandardError);
                }

                if (request.UseXrayUpstream)
                {
                    await WriteUpstreamDropIn(request.Channel, cancellationToken);
                }

                return new OpenFluxResult(true, null, Sanitize(result.StandardOutput));
            }
            finally
            {
                files.DeleteFile(configPath);
            }
        }
        catch (HttpRequestException)
        {
            return Failed("Не удалось скачать установщик OpenFlux.");
        }
    }

    public async Task<OpenFluxResult> RemoveAsync(string channel, CancellationToken cancellationToken)
    {
        if (!OpenFluxChannelValidator.IsChannel(channel))
        {
            return Failed("Недопустимое имя канала OpenFlux.");
        }

        if (IsDockerMode())
        {
            return await RemoveDockerAsync(channel, cancellationToken);
        }

        var result = await runner.RunAsync("bash", [InstallerPath, "remove", channel], TimeSpan.FromMinutes(2), cancellationToken);
        return result.ExitCode == 0
            ? new OpenFluxResult(true, null, Sanitize(result.StandardOutput))
            : Failed("Не удалось удалить канал OpenFlux.", result.StandardError);
    }

    public async Task<OpenFluxStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        if (IsDockerMode())
        {
            return await GetDockerStatusAsync(cancellationToken);
        }

        if (!files.FileExists(InstallerPath))
        {
            return new OpenFluxStatus(false, []);
        }

        var result = await runner.RunAsync("bash", [InstallerPath, "list"], TimeSpan.FromSeconds(20), cancellationToken);
        if (result.ExitCode != 0)
        {
            return new OpenFluxStatus(true, []);
        }

        return OpenFluxStatusParser.Parse(result.StandardOutput);
    }

    public static string BuildConfig(OpenFluxApplyRequest request)
    {
        var lines = new List<string>
        {
            $"channel={request.Channel}",
            $"key={request.Key}",
            $"port={request.Port}",
            "autoupdate=no",
            "mode=l4"
        };
        Add(lines, "vyandex", request.VyandexUrl);
        Add(lines, "yandex", request.YandexUrl);
        Add(lines, "mailru", request.MailruUrl);
        Add(lines, "telemost", request.TelemostUrl);
        Add(lines, "boards", request.BoardsUrl);
        Add(lines, "avito", request.AvitoUrl);
        Add(lines, "cupsonline", request.CupsUrl);
        Add(lines, "max_token", request.MaxToken);
        Add(lines, "max_uid", request.MaxUid?.ToString());
        if (!string.IsNullOrWhiteSpace(request.CookiesJson))
        {
            lines.Add($"cookies={Convert.ToBase64String(Encoding.UTF8.GetBytes(request.CookiesJson))}");
        }

        return string.Join('\n', lines) + '\n';
    }

    public static string BuildNodeConfig(OpenFluxApplyRequest request)
    {
        var lines = new List<string>
        {
            $"# OpenFlux node channel {request.Channel}, written by node-install.sh",
            "Role = exit",
            "Mode = l4",
            $"EncryptionKeyFile = {ConfigRoot}/{request.Channel}/encryption-key",
            $"CookieStore = /var/lib/openflux-node/{request.Channel}/cookies.json"
        };
        var context = FirstTransport(request);
        if (!string.IsNullOrWhiteSpace(context))
        {
            lines.Add($"URL = {context}");
        }

        AddTransport(lines, "vyandex", "vyandex", 100, request.VyandexUrl);
        AddTransport(lines, "yandex", "yandex", 95, request.YandexUrl);
        AddTransport(lines, "mailru", "mailru", 90, request.MailruUrl);
        AddTransport(lines, "telemost", "telemost", 85, request.TelemostUrl);
        AddTransport(lines, "boards", "boards", 80, request.BoardsUrl);
        AddTransport(lines, "avito", "avito", 75, request.AvitoUrl);
        AddTransport(lines, "cupsonline", "cupsonline", 70, request.CupsUrl);
        if (request.MaxToken is not null && request.MaxUid is not null)
        {
            lines.Add(string.Empty);
            lines.Add("[Transport oneme]");
            lines.Add("Type = oneme");
            lines.Add("Priority = 65");
            lines.Add($"MaxToken = {request.MaxToken}");
            lines.Add($"MaxUid = {request.MaxUid}");
        }
        lines.Add(string.Empty);
        lines.Add("[Transport direct]");
        lines.Add("Type = direct");
        lines.Add("Priority = 110");
        lines.Add($"Listen = 127.0.0.1:{request.Port}");
        return string.Join('\n', lines) + '\n';
    }

    private async Task<OpenFluxResult> ApplyDockerAsync(
        OpenFluxApplyRequest request,
        CancellationToken cancellationToken)
    {
        if (!OpenFluxChannelValidator.IsChannel(request.Channel))
        {
            return Failed("Недопустимое имя канала OpenFlux.");
        }

        try
        {
            var channelRoot = $"{ConfigRoot}/{request.Channel}";
            var stateRoot = $"/var/lib/openflux-node/{request.Channel}";
            files.CreateDirectory(ConfigRoot);
            files.CreateDirectory(channelRoot);
            files.CreateDirectory(SupervisorRoot);
            files.CreateDirectory(stateRoot);
            await files.WriteTextAsync(
                $"{channelRoot}/encryption-key",
                request.Key + "\n",
                cancellationToken);
            await files.WriteTextAsync(
                $"{channelRoot}/node.conf",
                BuildNodeConfig(request),
                cancellationToken);
            await files.WriteTextAsync(
                $"{SupervisorRoot}/openflux-{request.Channel}.conf",
                BuildSupervisorConfig(request.Channel),
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(request.CookiesJson))
            {
                await files.WriteTextAsync(
                    $"{stateRoot}/cookies.json",
                    request.CookiesJson,
                    cancellationToken);
            }
            await runner.RunAsync(
                "chmod",
                ["0640", $"{channelRoot}/node.conf", $"{channelRoot}/encryption-key"],
                TimeSpan.FromSeconds(10),
                cancellationToken);
            await runner.RunAsync(
                "supervisorctl",
                ["reread"],
                TimeSpan.FromSeconds(30),
                cancellationToken);
            var result = await runner.RunAsync(
                "supervisorctl",
                ["update"],
                TimeSpan.FromSeconds(30),
                cancellationToken);
            return result.ExitCode == 0
                ? new OpenFluxResult(true, null, string.Empty)
                : Failed("Не удалось запустить канал OpenFlux.", result.StandardError);
        }
        catch (Exception exception)
        {
            return Failed(exception.Message);
        }
    }

    private async Task<OpenFluxResult> RemoveDockerAsync(
        string channel,
        CancellationToken cancellationToken)
    {
        try
        {
            await services.StopAsync($"openflux-{channel}", cancellationToken);
            files.DeleteFile($"{SupervisorRoot}/openflux-{channel}.conf");
            files.DeleteFile($"{ConfigRoot}/{channel}/node.conf");
            files.DeleteFile($"{ConfigRoot}/{channel}/encryption-key");
            await runner.RunAsync("supervisorctl", ["reread"], TimeSpan.FromSeconds(30), cancellationToken);
            var result = await runner.RunAsync("supervisorctl", ["update"], TimeSpan.FromSeconds(30), cancellationToken);
            return result.ExitCode == 0
                ? new OpenFluxResult(true, null, string.Empty)
                : Failed("Не удалось обновить конфигурацию supervisor.", result.StandardError);
        }
        catch (Exception exception)
        {
            return Failed(exception.Message);
        }
    }

    private async Task<OpenFluxStatus> GetDockerStatusAsync(CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync("supervisorctl", ["status"], TimeSpan.FromSeconds(30), cancellationToken);
        return result.ExitCode == 0
            ? OpenFluxStatusParser.Parse(result.StandardOutput)
            : new OpenFluxStatus(true, []);
    }

    private static string BuildSupervisorConfig(string channel)
    {
        return $"[program:openflux-{channel}]\n" +
            $"command=/opt/openflux-node/bin/openflux --config /etc/openflux-node/{channel}/node.conf --upstream-proxy 127.0.0.1:10808\n" +
            "user=frogplug\n" +
            "environment=HOME=\"/var/lib/frogplug\"\n" +
            "autostart=true\nautorestart=true\n";
    }

    private static string? FirstTransport(OpenFluxApplyRequest request)
    {
        return request.VyandexUrl
            ?? request.YandexUrl
            ?? request.MailruUrl
            ?? request.TelemostUrl
            ?? request.BoardsUrl
            ?? request.AvitoUrl;
    }

    private static void AddTransport(
        List<string> lines,
        string name,
        string type,
        int priority,
        string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            lines.Add(string.Empty);
            lines.Add($"[Transport {name}]");
            lines.Add($"Type = {type}");
            lines.Add($"Priority = {priority}");
            lines.Add($"URL = {url}");
        }
    }

    private bool IsDockerMode()
    {
        return string.Equals(options.Value.ServiceMode, "docker", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> DownloadInstaller(OpenFluxApplyRequest request, CancellationToken cancellationToken)
    {
        var url = $"https://raw.githubusercontent.com/p1neappleXpress/OpenFlux/{request.OpenFluxRef}/deploy/node-install.sh";
        return await http.GetStringAsync(url, cancellationToken);
    }

    private static bool VerifyInstaller(string installer, string expected)
    {
        var normalized = expected.Trim().ToLowerInvariant();
        if (!OpenFluxChannelValidator.IsKey(normalized))
        {
            return false;
        }

        var actual = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(installer))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actual), Encoding.ASCII.GetBytes(normalized));
    }

    private async Task WriteUpstreamDropIn(string channel, CancellationToken cancellationToken)
    {
        var directory = $"/etc/systemd/system/openflux-node@{channel}.service.d";
        files.CreateDirectory(directory);
        // Флаги командной строки OpenFlux важнее значений из --config.
        // --upstream-proxy is accepted for role=exit and mode=l4, both set by node-install.sh.
        var content = "[Service]\nExecStart=\nExecStart=/opt/openflux-node/bin/openflux --config /etc/openflux-node/" +
            channel + "/node.conf --upstream-proxy 127.0.0.1:10808\n";
        await files.WriteTextAsync(Path.Combine(directory, "frogplug.conf"), content, cancellationToken);
        await services.ReloadManagerAsync(cancellationToken);
        await services.RestartAsync($"openflux-node@{channel}", cancellationToken);
    }

    private static void Add(List<string> lines, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            lines.Add($"{name}={value}");
        }
    }

    private static OpenFluxResult Failed(string error, string? output = null)
    {
        return new OpenFluxResult(false, error, Sanitize(output ?? string.Empty));
    }

    private static string Sanitize(string text)
    {
        var cleaned = Regex.Replace(text, "(?im)^(key|cookies)=.*$", "$1=[скрыто]");
        cleaned = Regex.Replace(cleaned, "(?i)\\\"(key|cookies)\\\"\\s*:\\s*\\\"[^\\\"]*\\\"", "\"$1\":\"[скрыто]\"");
        return cleaned.Length > 4000 ? cleaned[..4000] : cleaned;
    }
}
