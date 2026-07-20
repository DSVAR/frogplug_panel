using System.IO.Compression;
using System.Security.Cryptography;
using FrogPlug.Core;
using Microsoft.Extensions.Options;

namespace FrogPlug.Agent.Services;

public sealed class XrayUpdater
{
    private readonly IHttpClientFactory _clients;
    private readonly AgentOptions _options;
    private readonly IFileSystem _files;
    private readonly ICommandRunner _runner;
    private readonly XrayManager _manager;

    public XrayUpdater(IHttpClientFactory clients, IOptions<AgentOptions> options, IFileSystem files, ICommandRunner runner, XrayManager manager)
    {
        _clients = clients;
        _options = options.Value;
        _files = files;
        _runner = runner;
        _manager = manager;
    }

    // Обновление ядра Xray.
    public async Task<(bool Success, string Error, string OldVersion, string NewVersion)> UpdateAsync(
        string version,
        string? arch,
        string? downloadUrl,
        string? sha256,
        CancellationToken cancellationToken)
    {
        var client = _clients.CreateClient();
        var (zip, expected) = await DownloadAsync(client, version, arch, downloadUrl, sha256, cancellationToken);
        // Проверка контрольной суммы ядра.
        var actual = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        if (expected is null || !IsDigestMatch(expected, actual))
        {
            return (false, "Проверка SHA-256 не пройдена", "unknown", version);
        }
        var temp = _files.GetTempFileName();
        _files.WriteAllBytes(temp, zip);
        var extracted = ExtractBinary(temp);
        var old = _options.XrayBinary + ".old";
        var oldVersion = await ReadVersionAsync(_options.XrayBinary, cancellationToken);
        _files.CopyFile(_options.XrayBinary, old, true);
        _files.CopyFile(extracted, _options.XrayBinary, true);
        var check = await _runner.RunAsync(_options.XrayBinary, ["version"], TimeSpan.FromSeconds(20), cancellationToken);
        var restart = check.ExitCode == 0 ? await _manager.RestartAsync(cancellationToken) : check;
        if (restart.ExitCode != 0)
        {
            _files.CopyFile(old, _options.XrayBinary, true);
            await _manager.RestartAsync(cancellationToken);
            return (false, restart.StandardError, oldVersion, version);
        }
        return (true, string.Empty, oldVersion, version);
    }

    private static bool IsDigestMatch(string expected, string actual)
    {
        if (expected.Length != 64 || actual.Length != 64 || !expected.All(Uri.IsHexDigit))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expected),
            Convert.FromHexString(actual));
    }

    private static async Task<(byte[] Archive, string? Digest)> DownloadAsync(
        HttpClient client,
        string version,
        string? arch,
        string? downloadUrl,
        string? sha256,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(downloadUrl))
        {
            return (await client.GetByteArrayAsync(downloadUrl, cancellationToken), sha256?.Trim());
        }

        var suffix = arch?.Contains("arm", StringComparison.OrdinalIgnoreCase) == true ? "arm64-v8a" : "64";
        var baseUrl = $"https://github.com/XTLS/Xray-core/releases/download/{version}/Xray-linux-{suffix}";
        var archive = await client.GetByteArrayAsync(baseUrl + ".zip", cancellationToken);
        var digest = await client.GetStringAsync(baseUrl + ".zip.dgst", cancellationToken);
        return (archive, AgentParsers.ParseDigest(digest));
    }

    private string ExtractBinary(string archive)
    {
        var directory = Path.Combine(Path.GetTempPath(), "frogplug-xray-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        ZipFile.ExtractToDirectory(archive, directory);
        return Path.Combine(directory, "xray");
    }

    private async Task<string> ReadVersionAsync(string path, CancellationToken cancellationToken)
    {
        if (!_files.FileExists(path))
        {
            return "unknown";
        }
        var result = await _runner.RunAsync(path, ["version"], TimeSpan.FromSeconds(10), cancellationToken);
        return result.StandardOutput.Split('\n').FirstOrDefault()?.Trim() ?? "unknown";
    }
}
