using System.Security.Cryptography;
using FrogPlug.Core;
using Microsoft.Extensions.Options;

namespace FrogPlug.Agent.Services;

public sealed class GeoUpdater
{
    private readonly IHttpClientFactory _clients;
    private readonly AgentOptions _options;
    private readonly IFileSystem _files;
    private readonly XrayManager _manager;

    public GeoUpdater(IHttpClientFactory clients, IOptions<AgentOptions> options, IFileSystem files, XrayManager manager)
    {
        _clients = clients;
        _options = options.Value;
        _files = files;
        _manager = manager;
    }

    public async Task<bool> UpdateAsync(CancellationToken cancellationToken)
    {
        var success = await DownloadAsync(true, cancellationToken);
        if (success)
        {
            await _manager.RestartAsync(cancellationToken);
        }

        return success;
    }

    public Task<bool> EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        return HasAllFiles() ? Task.FromResult(true) : DownloadAsync(false, cancellationToken);
    }

    private bool HasAllFiles()
    {
        return GeoFileNames.All(name => _files.FileExists(Path.Combine(_options.GeoDir, name)));
    }

    private async Task<bool> DownloadAsync(bool replaceExisting, CancellationToken cancellationToken)
    {
        var client = _clients.CreateClient();
        foreach (var name in GeoFileNames)
        {
            var path = Path.Combine(_options.GeoDir, name);
            if (!replaceExisting && _files.FileExists(path))
            {
                continue;
            }

            var url = $"https://github.com/Loyalsoldier/v2ray-rules-dat/releases/latest/download/{name}";
            var data = await client.GetByteArrayAsync(url, cancellationToken);
            var checksum = await client.GetStringAsync(url + ".sha256sum", cancellationToken);
            var expected = AgentParsers.ParseSha256Sum(checksum);
            var actual = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
            if (expected is null || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual)))
            {
                return false;
            }

            _files.CreateDirectory(_options.GeoDir);
            _files.WriteAllBytes(path, data);
        }

        return true;
    }

    private static readonly string[] GeoFileNames = ["geoip.dat", "geosite.dat"];
}
