using FrogPlug.Core;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FrogPlug.Agent.Services;

// Применение конфигурации Hysteria2.
public sealed class HysteriaManager(
    IFileSystem files,
    ICommandRunner runner,
    IServiceController services,
    IOptions<AgentOptions> options)
{
    private const string ConfigPath = "/etc/hysteria/config.yaml";
    private const string CertificateRoot = "/var/lib/frogplug/.local/share/caddy/certificates";
    private const string SelfSignedRoot = "/etc/hysteria";
    private const string UnitPath = "/etc/systemd/system/hysteria.service";

    public async Task<HysteriaApplyResult> ApplyAsync(HysteriaApplyRequest request, CancellationToken cancellationToken)
    {
        if (!request.Enabled)
        {
            await services.StopAsync("hysteria", cancellationToken);
            return new HysteriaApplyResult(true);
        }

        var certificate = string.IsNullOrWhiteSpace(request.Domain)
            ? await EnsureSelfSignedAsync(request.RealitySni ?? "localhost", cancellationToken)
            : await FindFile($"{request.Domain}.crt", cancellationToken);
        var key = string.IsNullOrWhiteSpace(request.Domain)
            ? Path.Combine(SelfSignedRoot, "self-signed.key")
            : await FindFile($"{request.Domain}.key", cancellationToken);
        if (certificate is null || key is null)
        {
            return new HysteriaApplyResult(false, "Не удалось подготовить сертификат Hysteria2");
        }

        var content = HysteriaConfigBuilder.Build(certificate, key, request.Port, request.Users, request.ObfsPassword);
        var unchanged = await FileContent.EqualsNormalizedAsync(files, ConfigPath, content, cancellationToken);
        if (unchanged)
        {
            // После перезапуска контейнера конфиг на месте, а процесс не запущен — поднимаем его.
            var active = await services.IsActiveAsync("hysteria", cancellationToken);
            if (active.ExitCode == 0)
            {
                return new HysteriaApplyResult(true, PinSha256: await ReadFingerprintAsync(certificate, cancellationToken));
            }

            var started = await services.StartAsync("hysteria", cancellationToken);
            return started.ExitCode == 0
                ? new HysteriaApplyResult(true, PinSha256: await ReadFingerprintAsync(certificate, cancellationToken))
                : new HysteriaApplyResult(false, "Не удалось запустить Hysteria2");
        }

        files.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        await files.WriteTextAsync(ConfigPath, content, cancellationToken);
        await runner.RunAsync("chmod", ["600", ConfigPath], TimeSpan.FromSeconds(10), cancellationToken);
        if (string.Equals(options.Value.ServiceMode, "systemd", StringComparison.OrdinalIgnoreCase))
        {
            await files.WriteTextAsync(UnitPath, BuildUnit(), cancellationToken);
            await services.ReloadManagerAsync(cancellationToken);
        }

        var result = await services.RestartAsync("hysteria", cancellationToken);
        return result.ExitCode == 0
            ? new HysteriaApplyResult(true, PinSha256: await ReadFingerprintAsync(certificate, cancellationToken))
            : new HysteriaApplyResult(false, "Не удалось запустить Hysteria2");
    }

    private async Task<string?> EnsureSelfSignedAsync(string host, CancellationToken cancellationToken)
    {
        var certificatePath = Path.Combine(SelfSignedRoot, "self-signed.crt");
        var keyPath = Path.Combine(SelfSignedRoot, "self-signed.key");
        if (!files.FileExists(certificatePath) || !files.FileExists(keyPath))
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = new CertificateRequest($"CN={host}", key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
            using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddYears(10));
            files.CreateDirectory(SelfSignedRoot);
            await files.WriteTextAsync(certificatePath, certificate.ExportCertificatePem(), cancellationToken);
            await files.WriteTextAsync(keyPath, key.ExportPkcs8PrivateKeyPem(), cancellationToken);
        }

        await Task.CompletedTask;
        return certificatePath;
    }

    private async Task<string> ReadFingerprintAsync(string certificatePath, CancellationToken cancellationToken)
    {
        using var certificate = X509Certificate2.CreateFromPem(await files.ReadTextAsync(certificatePath, cancellationToken));
        var fingerprint = Convert.ToHexString(SHA256.HashData(certificate.RawData));
        return string.Join(':', Enumerable.Range(0, fingerprint.Length / 2).Select(i => fingerprint.Substring(i * 2, 2)));
    }

    private async Task<string?> FindFile(string name, CancellationToken cancellationToken)
    {
        var result = await runner.RunAsync(
            "find",
            [CertificateRoot, "-type", "f", "-name", name, "-print", "-quit"],
            TimeSpan.FromSeconds(10),
            cancellationToken);
        return result.ExitCode == 0 ? result.StandardOutput.Trim().Split('\n').FirstOrDefault() : null;
    }

    private string BuildUnit()
    {
        return "[Unit]\nDescription=Hysteria2\nAfter=network-online.target\n\n" +
            "[Service]\nType=simple\nUser=frogplug\n" +
            $"ExecStart={options.Value.HysteriaBinary} server -c {ConfigPath}\n" +
            "Restart=always\nRestartSec=5\n\n[Install]\nWantedBy=multi-user.target\n";
    }
}
