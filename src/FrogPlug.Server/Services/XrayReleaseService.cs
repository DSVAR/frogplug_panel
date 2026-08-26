using System.Net.Http.Json;

namespace FrogPlug.Server.Services;

public sealed record XrayRelease(string TagName, bool Prerelease);

public sealed class XrayReleaseService(IHttpClientFactory factory)
{
    private DateTimeOffset cacheUntil;
    private IReadOnlyList<XrayRelease> cache = [];

    public async Task<IReadOnlyList<XrayRelease>> GetLatest(CancellationToken cancellationToken = default)
    {
        if (cacheUntil > DateTimeOffset.UtcNow)
        {
            return cache;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/repos/XTLS/Xray-core/releases");
        request.Headers.UserAgent.ParseAdd("panel");
        var client = factory.CreateClient();
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<List<XrayRelease>>(cancellationToken: cancellationToken) ?? [];
        cache = data.Take(10).ToArray();
        cacheUntil = DateTimeOffset.UtcNow.AddHours(1);
        return cache;
    }
}
