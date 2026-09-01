using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using FrogPlug.Core;

namespace FrogPlug.Server.Services;

// Хранилище проверенных версий ядра Xray.
public sealed class XrayCoreStore
{
    private static readonly Regex VersionPattern = new("^v\\d+\\.\\d+\\.\\d+$", RegexOptions.CultureInvariant);
    private static readonly string[] SupportedRids = ["linux-x64", "linux-arm64"];
    private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();
    private readonly IHttpClientFactory clients;
    private readonly string storagePath;
    private readonly string bundledPath;
    private readonly string defaultVersion;
    private readonly Lazy<Task> import;

    public XrayCoreStore(IHttpClientFactory clients, IConfiguration configuration, IWebHostEnvironment environment)
    {
        this.clients = clients;
        var dataDirectory = configuration["DataDirectory"] ?? Path.Combine(environment.ContentRootPath, "appdata");
        storagePath = Path.Combine(dataDirectory, "xray-core");
        bundledPath = configuration["XrayCore:BundledPath"] ?? Path.Combine(environment.ContentRootPath, "artifacts", "xray-core");
        if (!Path.IsPathRooted(bundledPath))
        {
            bundledPath = Path.Combine(environment.ContentRootPath, bundledPath);
        }

        defaultVersion = configuration["XrayDefaultVersion"] ?? ReadDefaultVersion(environment.ContentRootPath);
        import = new Lazy<Task>(ImportBundledAsync);
    }

    public string DefaultVersion => defaultVersion;

    public async Task<IReadOnlyList<XrayCoreVersion>> ListAsync()
    {
        await import.Value;
        if (!Directory.Exists(storagePath))
        {
            return [];
        }

        return Directory.GetDirectories(storagePath)
            .Select(path => new XrayCoreVersion(Path.GetFileName(path), ListRids(path)))
            .Where(item => IsVersion(item.Version) && item.Rids.Count > 0)
            .OrderByDescending(item => item.Version, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<XrayCoreFile?> GetAsync(string version, string rid)
    {
        if (!IsValid(version, rid))
        {
            return null;
        }

        await import.Value;
        var asset = AssetName(rid);
        var path = Path.Combine(storagePath, version, rid, asset);
        var checksumPath = path + ".sha256";
        if (!File.Exists(path) || !File.Exists(checksumPath))
        {
            return null;
        }

        var checksum = (await File.ReadAllTextAsync(checksumPath)).Trim().ToLowerInvariant();
        return IsSha256(checksum) ? new XrayCoreFile(path, checksum) : null;
    }

    public async Task<XrayCoreFile> EnsureAsync(string version, string rid, CancellationToken cancellationToken = default)
    {
        if (!IsValid(version, rid))
        {
            throw new ArgumentException("Недопустимая версия или архитектура Xray");
        }

        var existing = await GetAsync(version, rid);
        if (existing is not null)
        {
            return existing;
        }

        var key = version + "/" + rid;
        var gate = locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            existing = await GetAsync(version, rid);
            if (existing is not null)
            {
                return existing;
            }

            return await DownloadAsync(version, rid, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<XrayCoreFile> DownloadAsync(string version, string rid, CancellationToken cancellationToken)
    {
        var asset = AssetName(rid);
        var url = $"https://github.com/XTLS/Xray-core/releases/download/{version}/{asset}";
        var client = clients.CreateClient();
        using var digestRequest = CreateRequest(url + ".dgst");
        using var digestResponse = await client.SendAsync(digestRequest, cancellationToken);
        digestResponse.EnsureSuccessStatusCode();
        var expected = AgentParsers.ParseDigest(await digestResponse.Content.ReadAsStringAsync(cancellationToken));
        if (expected is null)
        {
            throw new InvalidDataException("Не найдена SHA-256 сумма Xray");
        }

        using var archiveRequest = CreateRequest(url);
        using var archiveResponse = await client.SendAsync(archiveRequest, cancellationToken);
        archiveResponse.EnsureSuccessStatusCode();
        var archive = await archiveResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        var actual = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
        if (!FixedEquals(expected, actual))
        {
            throw new InvalidDataException("Контрольная сумма Xray не совпала");
        }

        return await StoreAsync(version, rid, asset, archive, actual, cancellationToken);
    }

    private async Task<XrayCoreFile> StoreAsync(
        string version,
        string rid,
        string asset,
        byte[] archive,
        string checksum,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(storagePath, version, rid);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, asset);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, archive, cancellationToken);
            File.Move(temporary, path, true);
            await File.WriteAllTextAsync(path + ".sha256", checksum + Environment.NewLine, cancellationToken);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        return new XrayCoreFile(path, checksum);
    }

    private async Task ImportBundledAsync()
    {
        if (!Directory.Exists(bundledPath))
        {
            return;
        }

        foreach (var versionDirectory in Directory.GetDirectories(bundledPath))
        {
            var version = Path.GetFileName(versionDirectory);
            if (!IsVersion(version))
            {
                continue;
            }

            foreach (var rid in SupportedRids)
            {
                await ImportBundledRidAsync(version, rid, versionDirectory);
            }
        }
    }

    private async Task ImportBundledRidAsync(string version, string rid, string versionDirectory)
    {
        var asset = AssetName(rid);
        var source = Path.Combine(versionDirectory, rid, asset);
        var sourceChecksum = source + ".sha256";
        if (!File.Exists(source) || !File.Exists(sourceChecksum) || await GetAsyncWithoutImport(version, rid) is not null)
        {
            return;
        }

        var checksum = (await File.ReadAllTextAsync(sourceChecksum)).Trim().ToLowerInvariant();
        var archive = await File.ReadAllBytesAsync(source);
        var actual = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
        if (!IsSha256(checksum) || !FixedEquals(checksum, actual))
        {
            throw new InvalidDataException("Контрольная сумма встроенного Xray не совпала");
        }

        await StoreAsync(version, rid, asset, archive, checksum, CancellationToken.None);
    }

    private async Task<XrayCoreFile?> GetAsyncWithoutImport(string version, string rid)
    {
        var path = Path.Combine(storagePath, version, rid, AssetName(rid));
        var checksumPath = path + ".sha256";
        if (!File.Exists(path) || !File.Exists(checksumPath))
        {
            return null;
        }

        var checksum = (await File.ReadAllTextAsync(checksumPath)).Trim().ToLowerInvariant();
        return IsSha256(checksum) ? new XrayCoreFile(path, checksum) : null;
    }

    private static HttpRequestMessage CreateRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd("FrogPlug");
        return request;
    }

    private static IReadOnlyList<string> ListRids(string versionPath)
    {
        return SupportedRids.Where(rid => File.Exists(Path.Combine(versionPath, rid, AssetName(rid)))).ToArray();
    }

    private static string ReadDefaultVersion(string contentRoot)
    {
        var path = Path.Combine(contentRoot, "deploy", "xray-core.json");
        if (!File.Exists(path))
        {
            return "v25.10.15";
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("version").GetString() ?? "v25.10.15";
    }

    private static bool IsValid(string version, string rid) => IsVersion(version) && SupportedRids.Contains(rid, StringComparer.Ordinal);

    private static bool IsVersion(string version) => VersionPattern.IsMatch(version);

    private static string AssetName(string rid) => rid == "linux-x64" ? "Xray-linux-64.zip" : "Xray-linux-arm64-v8a.zip";

    private static bool IsSha256(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool FixedEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(left), Convert.FromHexString(right));
    }
}
