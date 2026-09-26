using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using FrogPlug.Core;
using Microsoft.Extensions.Options;

namespace FrogPlug.Agent.Services;

public sealed record MtgResult(bool Success, string? Error = null);

// Применение конфигурации MTProto.
public sealed class MtgManager(
    IFileSystem files,
    ICommandRunner runner,
    HttpClient http,
    IServiceController services,
    IOptions<AgentOptions> options)
{
    private const string ConfigPath = "/etc/mtg.toml";
    private const string UnitPath = "/etc/systemd/system/mtg.service";

    public MtgManager(IFileSystem files, ICommandRunner runner, HttpClient http)
        : this(
            files,
            runner,
            http,
            new SystemdServiceController(runner),
            Options.Create(new AgentOptions()))
    {
    }

    public async Task<MtgResult> ApplyAsync(MtprotoApplyRequest request, CancellationToken cancellationToken)
    {
        if (!request.Enabled)
        {
            await services.StopAsync("mtg", cancellationToken);
            return new MtgResult(true);
        }

        if (IsDockerMode())
        {
            return await ApplyDockerAsync(request, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.Sha256))
        {
            return new MtgResult(false, "Не указана SHA-256 сумма mtg");
        }

        if (!IsSha256(request.Sha256))
        {
            return new MtgResult(false, "SHA-256 сумма mtg имеет неверный формат");
        }

        try
        {
            MtprotoSecret.Parse(request.Secret ?? string.Empty);
            var content = MtgConfigBuilder.Build(
                request.Secret!,
                request.Port,
                request.FrontingPort,
                request.FrontingHost);
            if (await FileContent.EqualsNormalizedAsync(files, ConfigPath, content, cancellationToken))
            {
                return new MtgResult(true);
            }

            await InstallAsync(request, cancellationToken);
            await files.WriteTextAsync(ConfigPath, content, cancellationToken);
            await runner.RunAsync("chmod", ["600", ConfigPath], TimeSpan.FromSeconds(10), cancellationToken);
            if (string.Equals(options.Value.ServiceMode, "systemd", StringComparison.OrdinalIgnoreCase))
            {
                await files.WriteTextAsync(UnitPath, BuildUnit(options.Value.MtgBinary), cancellationToken);
                await services.ReloadManagerAsync(cancellationToken);
            }

            var result = await services.StartAsync("mtg", cancellationToken);
            if (result.ExitCode != 0)
            {
                return new MtgResult(false, "Не удалось запустить mtg");
            }

            return new MtgResult(true);
        }
        catch (Exception exception)
        {
            return new MtgResult(false, exception.Message);
        }
    }

    public async Task<ComponentStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var active = await services.IsActiveAsync("mtg", cancellationToken);
        return new ComponentStatus(files.FileExists(options.Value.MtgBinary), active.ExitCode == 0);
    }

    private async Task InstallAsync(MtprotoApplyRequest request, CancellationToken cancellationToken)
    {
        var archive = await http.GetByteArrayAsync(BuildDownloadUrl(request.Version), cancellationToken);
        var hash = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.ASCII.GetBytes(hash),
                System.Text.Encoding.ASCII.GetBytes(request.Sha256!.Trim().ToLowerInvariant())))
        {
            throw new InvalidOperationException("SHA-256 архива mtg не совпадает");
        }

        using var input = new MemoryStream(archive);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            if (Path.GetFileName(entry.Name) != "mtg" || entry.DataStream is null)
            {
                continue;
            }

            using var binary = new MemoryStream();
            await entry.DataStream.CopyToAsync(binary, cancellationToken);
            files.WriteAllBytes(options.Value.MtgBinary, binary.ToArray());
            await runner.RunAsync(
                "chmod",
                ["755", options.Value.MtgBinary],
                TimeSpan.FromSeconds(10),
                cancellationToken);
            return;
        }

        throw new InvalidOperationException("В архиве mtg не найден исполняемый файл");
    }

    private static string BuildDownloadUrl(string version)
    {
        var arch = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "amd64";
        return BuildDownloadUrl(version, arch);
    }

    // В имени архива версия без префикса «v»: mtg-2.3.0-linux-amd64.tar.gz.
    public static string BuildDownloadUrl(string version, string arch)
    {
        return $"https://github.com/9seconds/mtg/releases/download/{version}/mtg-{version.TrimStart('v')}-linux-{arch}.tar.gz";
    }

    private async Task<MtgResult> ApplyDockerAsync(
        MtprotoApplyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            MtprotoSecret.Parse(request.Secret ?? string.Empty);
            var content = MtgConfigBuilder.Build(
                request.Secret!,
                request.Port,
                request.FrontingPort,
                request.FrontingHost);
            var exists = files.FileExists(ConfigPath);
            var changed = !await FileContent.EqualsNormalizedAsync(files, ConfigPath, content, cancellationToken);
            if (changed)
            {
                await files.WriteTextAsync(ConfigPath, content, cancellationToken);
                await runner.RunAsync("chmod", ["600", ConfigPath], TimeSpan.FromSeconds(10), cancellationToken);
            }

            var result = exists
                ? await services.RestartAsync("mtg", cancellationToken)
                : await services.StartAsync("mtg", cancellationToken);
            return result.ExitCode == 0
                ? new MtgResult(true)
                : new MtgResult(false, "Не удалось запустить mtg");
        }
        catch (Exception exception)
        {
            return new MtgResult(false, exception.Message);
        }
    }

    private bool IsDockerMode()
    {
        return string.Equals(options.Value.ServiceMode, "docker", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildUnit(string binaryPath)
    {
        return "[Unit]\nDescription=mtg MTProto proxy\nAfter=network-online.target\n\n" +
            $"[Service]\nType=simple\nExecStart={binaryPath} run /etc/mtg.toml\nRestart=always\n" +
            "RestartSec=5\n\n[Install]\nWantedBy=multi-user.target\n";
    }

    private static bool IsSha256(string value)
    {
        return value.Trim().Length == 64 && value.Trim().All(Uri.IsHexDigit);
    }
}
