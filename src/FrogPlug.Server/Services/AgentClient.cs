using FrogPlug.Core;
using FrogPlug.Server.Data;

namespace FrogPlug.Server.Services;

public interface IAgentClient
{
    Task<AgentStatus?> GetStatus(Node node, CancellationToken cancellationToken = default);
    Task<TrafficStats?> GetStats(Node node, bool reset, CancellationToken cancellationToken = default);
    Task ApplyXrayConfig(Node node, string configJson, CancellationToken cancellationToken = default);
    Task<AgentUpdateResult?> UpdateXray(
        Node node,
        string version,
        string? downloadUrl = null,
        string? sha256 = null,
        CancellationToken cancellationToken = default);
    Task UpdateGeo(Node node, CancellationToken cancellationToken = default);
    Task ApplyCaddy(Node node, CaddyApplyRequest request, CancellationToken cancellationToken = default);
    Task ApplyHaproxy(Node node, HaproxyApplyRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    Task ApplySite(Node node, SiteApplyRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
    Task ApplyOpenFlux(Node node, OpenFluxApplyRequest request, CancellationToken cancellationToken = default);
    Task RemoveOpenFlux(Node node, string channel, CancellationToken cancellationToken = default);
    Task ApplyMtproto(Node node, MtprotoApplyRequest request, CancellationToken cancellationToken = default);
    Task ApplyTgWeb(Node node, TgWebApplyRequest request, CancellationToken cancellationToken = default);
    Task<HysteriaApplyResult?> ApplyHysteria(Node node, HysteriaApplyRequest request, CancellationToken cancellationToken = default);
    Task<SniScanResponse?> ScanMask(Node node, string? range = null, int? limit = null, CancellationToken cancellationToken = default)
        => Task.FromResult<SniScanResponse?>(null);
}

public sealed record AgentUpdateResult(string? OldVersion, string? NewVersion);

public sealed class AgentClient(AgentTunnelRegistry tunnels) : IAgentClient
{
    public Task<AgentStatus?> GetStatus(Node node, CancellationToken cancellationToken = default)
    {
        return Send<AgentStatus>(node, HttpMethod.Get, "/status", null, cancellationToken);
    }

    public Task<TrafficStats?> GetStats(Node node, bool reset, CancellationToken cancellationToken = default)
    {
        var path = $"/xray/stats?reset={reset.ToString().ToLowerInvariant()}";
        return Send<TrafficStats>(node, HttpMethod.Get, path, null, cancellationToken);
    }

    public async Task ApplyXrayConfig(Node node, string configJson, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/xray/config", new XrayConfigRequest(configJson), cancellationToken);
    }

    public Task<AgentUpdateResult?> UpdateXray(
        Node node,
        string version,
        string? downloadUrl = null,
        string? sha256 = null,
        CancellationToken cancellationToken = default)
    {
        var request = new XrayUpdateRequest(version, null, downloadUrl, sha256);
        return Send<AgentUpdateResult>(node, HttpMethod.Post, "/xray/update", request, cancellationToken);
    }

    public async Task UpdateGeo(Node node, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/geo/update", null, cancellationToken);
    }

    public async Task ApplyCaddy(Node node, CaddyApplyRequest request, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/caddy/apply", request, cancellationToken);
    }

    public async Task ApplyHaproxy(Node node, HaproxyApplyRequest request, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/haproxy/apply", request, cancellationToken);
    }

    public async Task ApplySite(Node node, SiteApplyRequest request, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/site/apply", request, cancellationToken);
    }

    public async Task ApplyOpenFlux(Node node, OpenFluxApplyRequest request, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/openflux/apply", request, cancellationToken);
    }

    public async Task RemoveOpenFlux(Node node, string channel, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/openflux/remove", new OpenFluxRemoveRequest(channel), cancellationToken);
    }

    public async Task ApplyMtproto(Node node, MtprotoApplyRequest request, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/mtproto/apply", request, cancellationToken);
    }

    public async Task ApplyTgWeb(Node node, TgWebApplyRequest request, CancellationToken cancellationToken = default)
    {
        await Send<object>(node, HttpMethod.Post, "/tgweb/apply", request, cancellationToken);
    }

    public async Task<HysteriaApplyResult?> ApplyHysteria(Node node, HysteriaApplyRequest request, CancellationToken cancellationToken = default)
    {
        return await Send<HysteriaApplyResult>(node, HttpMethod.Post, "/hysteria/apply", request, cancellationToken);
    }

    public Task<SniScanResponse?> ScanMask(
        Node node,
        string? range = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return Send<SniScanResponse>(
            node,
            HttpMethod.Post,
            "/mask/scan",
            new SniScanRequest(range, limit),
            cancellationToken);
    }

    private async Task<T?> Send<T>(Node node, HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        return await tunnels.SendAsync<T>(
            node.Id,
            method.Method,
            path,
            body,
            TimeoutFor(path),
            cancellationToken);
    }

    // Опрос статуса должен быть быстрым. Остальные запросы запускают сервисы и скачивают файлы,
    // на ноде с одним ядром это легко дольше 30 секунд.
    public static TimeSpan TimeoutFor(string path)
    {
        if (path.StartsWith("/status", StringComparison.Ordinal))
        {
            return TimeSpan.FromSeconds(30);
        }

        return path.StartsWith("/xray/update", StringComparison.Ordinal)
            ? TimeSpan.FromMinutes(5)
            : TimeSpan.FromMinutes(3);
    }
}
