using FrogPlug.Core;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class FakeAgentClient : IAgentClient
{
    public Dictionary<string, AgentStatus?> Statuses { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, TrafficStats?> Stats { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ThrowOnStatus { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ThrowOnStats { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ThrowOnApplyXray { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> StatusCalls { get; } = [];
    public List<string> StatsCalls { get; } = [];
    public List<(string Node, string Config)> AppliedConfigs { get; } = [];
    public List<(string Node, CaddyApplyRequest Request)> AppliedCaddy { get; } = [];
    public List<(string Node, OpenFluxApplyRequest Request)> AppliedOpenFlux { get; } = [];
    public List<(string Node, string Channel)> RemovedOpenFlux { get; } = [];
    public List<(string Node, MtprotoApplyRequest Request)> AppliedMtproto { get; } = [];
    public List<(string Node, TgWebApplyRequest Request)> AppliedTgWeb { get; } = [];
    public List<(string Node, HysteriaApplyRequest Request)> AppliedHysteria { get; } = [];
    public SniScanResponse? ScanResult { get; set; }

    public Task<AgentStatus?> GetStatus(Node node, CancellationToken cancellationToken = default)
    {
        StatusCalls.Add(node.Name);
        if (ThrowOnStatus.Contains(node.Name))
        {
            throw new HttpRequestException("status failure");
        }

        return Task.FromResult(Statuses.GetValueOrDefault(node.Name));
    }

    public Task<TrafficStats?> GetStats(Node node, bool reset, CancellationToken cancellationToken = default)
    {
        StatsCalls.Add(node.Name);
        if (ThrowOnStats.Contains(node.Name))
        {
            throw new HttpRequestException("stats failure");
        }

        return Task.FromResult(Stats.GetValueOrDefault(node.Name));
    }

    public Task ApplyXrayConfig(Node node, string configJson, CancellationToken cancellationToken = default)
    {
        if (ThrowOnApplyXray.Contains(node.Name))
        {
            throw new HttpRequestException("apply failure");
        }

        AppliedConfigs.Add((node.Name, configJson));
        return Task.CompletedTask;
    }

    public Task<AgentUpdateResult?> UpdateXray(
        Node node,
        string version,
        string? downloadUrl = null,
        string? sha256 = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AgentUpdateResult?>(null);
    }

    public Task UpdateGeo(Node node, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ApplyCaddy(Node node, CaddyApplyRequest request, CancellationToken cancellationToken = default)
    {
        AppliedCaddy.Add((node.Name, request));
        return Task.CompletedTask;
    }

    public Task ApplyOpenFlux(Node node, OpenFluxApplyRequest request, CancellationToken cancellationToken = default)
    {
        AppliedOpenFlux.Add((node.Name, request));
        return Task.CompletedTask;
    }

    public Task RemoveOpenFlux(Node node, string channel, CancellationToken cancellationToken = default)
    {
        RemovedOpenFlux.Add((node.Name, channel));
        return Task.CompletedTask;
    }

    public Task ApplyMtproto(Node node, MtprotoApplyRequest request, CancellationToken cancellationToken = default)
    {
        AppliedMtproto.Add((node.Name, request));
        return Task.CompletedTask;
    }

    public Task ApplyTgWeb(Node node, TgWebApplyRequest request, CancellationToken cancellationToken = default)
    {
        AppliedTgWeb.Add((node.Name, request));
        return Task.CompletedTask;
    }

    public Task<HysteriaApplyResult?> ApplyHysteria(Node node, HysteriaApplyRequest request, CancellationToken cancellationToken = default)
    {
        AppliedHysteria.Add((node.Name, request));
        return Task.FromResult<HysteriaApplyResult?>(new HysteriaApplyResult(true));
    }

    public Task<SniScanResponse?> ScanMask(
        Node node,
        string? range = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ScanResult);
    }
}
