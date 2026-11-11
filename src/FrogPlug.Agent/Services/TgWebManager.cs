using System.Text.Json;
using FrogPlug.Core;
using Microsoft.Extensions.Options;

namespace FrogPlug.Agent.Services;

public sealed record TgWebResult(bool Success, string? Error = null);

// Подмена на настоящий сайт для Telegram Web.
public sealed class TgWebManager(
    IFileSystem files,
    ICommandRunner runner,
    IServiceController services,
    IOptions<AgentOptions> options)
{
    private const string DirectoryPath = "/etc/tproxy-server";
    private const string ConfigPath = DirectoryPath + "/config.json";
    private const string ProfilesPath = DirectoryPath + "/profiles.json";
    private const string UnitPath = "/etc/systemd/system/tgweb.service";
    private const string MtProxyDirectoryPath = "/etc/mtproxy";
    private const string MtProxyStartPath = MtProxyDirectoryPath + "/start.sh";
    private const string MtProxyUnitPath = "/etc/systemd/system/mtproxy.service";

    public async Task<TgWebResult> ApplyAsync(TgWebApplyRequest request, CancellationToken cancellationToken)
    {
        if (!request.Enabled)
        {
            await services.StopAsync("tgweb", cancellationToken);
            await services.StopAsync("mtproxy", cancellationToken);
            return new TgWebResult(true);
        }

        try
        {
            var secret = TgWebLinks.NormalizeSecret(request.Secret);
            var config = JsonSerializer.Serialize(new
            {
                public_hostname = request.Hostname,
                listen = "127.0.0.1:8080",
                admin_listen = "127.0.0.1:8081",
                public_dir = "/var/lib/frogplug/site",
                profiles_file = ProfilesPath
            }) + "\n";
            var profiles = JsonSerializer.Serialize(new
            {
                profiles = new[]
                {
                    new
                    {
                        name = "default",
                        secret,
                        backend = $"127.0.0.1:{request.BackendPort}",
                        carrier_mode = "https"
                    }
                }
            }) + "\n";
            var mtProxyScript = MtProxyConfigBuilder.BuildStartScript(request.Secret);
            files.CreateDirectory(DirectoryPath);
            files.CreateDirectory(MtProxyDirectoryPath);
            var changed = !await FileContent.EqualsNormalizedAsync(files, ConfigPath, config, cancellationToken) ||
                !await FileContent.EqualsNormalizedAsync(files, ProfilesPath, profiles, cancellationToken) ||
                !await FileContent.EqualsNormalizedAsync(files, MtProxyStartPath, mtProxyScript, cancellationToken);
            if (changed)
            {
                await files.WriteTextAsync(ConfigPath, config, cancellationToken);
                await files.WriteTextAsync(ProfilesPath, profiles, cancellationToken);
                await files.WriteTextAsync(MtProxyStartPath, mtProxyScript, cancellationToken);
                await runner.RunAsync(
                    "chmod",
                    ["600", ConfigPath, ProfilesPath],
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
                await runner.RunAsync(
                    "chmod",
                    ["700", MtProxyStartPath],
                    TimeSpan.FromSeconds(10),
                    cancellationToken);
                if (!IsDockerMode())
                {
                    await files.WriteTextAsync(UnitPath, BuildUnit(), cancellationToken);
                    await files.WriteTextAsync(MtProxyUnitPath, BuildMtProxyUnit(), cancellationToken);
                    await services.ReloadManagerAsync(cancellationToken);
                }
            }

            var result = await EnsureStartedAsync("mtproxy", changed, cancellationToken);
            if (result.ExitCode == 0)
            {
                result = await EnsureStartedAsync("tgweb", changed, cancellationToken);
            }
            return result.ExitCode == 0
                ? new TgWebResult(true)
                : new TgWebResult(false, "Не удалось запустить tgweb");
        }
        catch (Exception exception)
        {
            return new TgWebResult(false, exception.Message);
        }
    }

    private bool IsDockerMode()
    {
        return options.Value.ServiceMode.Equals("docker", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<CommandResult> EnsureStartedAsync(
        string service,
        bool changed,
        CancellationToken cancellationToken)
    {
        var result = changed
            ? await services.RestartAsync(service, cancellationToken)
            : await services.IsActiveAsync(service, cancellationToken);
        return !changed && result.ExitCode != 0
            ? await services.StartAsync(service, cancellationToken)
            : result;
    }

    private static string BuildUnit()
    {
        return "[Unit]\nDescription=Telegram WEB proxy relay\nAfter=network-online.target\n\n" +
            "[Service]\nType=simple\nExecStart=/usr/local/bin/tproxy-server -config /etc/tproxy-server/config.json " +
            "-profiles-file /etc/tproxy-server/profiles.json\nRestart=always\nRestartSec=5\n\n" +
            "[Install]\nWantedBy=multi-user.target\n";
    }

    private static string BuildMtProxyUnit()
    {
        return "[Unit]\nDescription=Official Telegram MTProxy for Telegram WEB relay\n" +
            "After=network-online.target\n\n[Service]\nType=simple\n" +
            "ExecStart=/etc/mtproxy/start.sh\nRestart=always\nRestartSec=5\n\n" +
            "[Install]\nWantedBy=multi-user.target\n";
    }
}
