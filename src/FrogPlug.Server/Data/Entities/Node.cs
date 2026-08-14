using FrogPlug.Core;

namespace FrogPlug.Server.Data;

[Flags]
public enum NodeRole
{
    Entry = 1,
    Exit = 2,
    RuExit = 4,
    GoogleExit = 8,
    WhitelistEntry = 16
}

public sealed class Node
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Address { get; set; }
    public required string Country { get; set; }
    public NodeRole Role { get; set; }
    public string? Domain { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool RealityEnabled { get; set; } = true;
    public int RealityPort { get; set; } = NodePorts.Reality;
    public required string RealityPrivateKey { get; set; }
    public required string RealityPublicKey { get; set; }
    public required string RealityShortId { get; set; }
    public string RealityServerName { get; set; } = string.Empty;
    public string RealityDest { get; set; } = string.Empty;
    public MaskMode MaskMode { get; set; } = MaskMode.OwnDomain;
    public string? RealitySni { get; set; }
    public string RealityFingerprint { get; set; } = "chrome";
    public string XhttpMode { get; set; } = "auto";
    public bool HysteriaObfsEnabled { get; set; } = true;
    public int? WhitelistExitNodeId { get; set; }
    public string XhttpPath { get; set; } = string.Empty;
    public bool XhttpEnabled { get; set; } = true;
    public int XhttpPort { get; set; } = NodePorts.Xhttp;
    public bool MtprotoEnabled { get; set; }
    public bool TgWebEnabled { get; set; }
    public bool HysteriaEnabled { get; set; }
    public int HysteriaPort { get; set; } = NodePorts.Hysteria;
    public string? HysteriaPinSha256 { get; set; }
    public int MtprotoPort { get; set; } = NodePorts.Mtproto;
    public string? MtprotoSecret { get; set; }
    public int MtprotoFrontingPort { get; set; } = NodePorts.CaddyTls;
    public string? MtprotoHost { get; set; }

    // MTProto отделяется от Reality только по SNI, поэтому ему нужен свой поддомен, отличный от домена ноды.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? MtprotoRouteHost => string.IsNullOrWhiteSpace(MtprotoHost) ||
        string.Equals(MtprotoHost.Trim(), Domain?.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : MtprotoHost.Trim();
    public string StubTemplate { get; set; } = StubSiteTemplates.PersonalBlog;
    public string? StubTitle { get; set; }
    public Guid CascadeUuid { get; set; } = Guid.NewGuid();
    public required string AgentUrl { get; set; }
    public required string AgentToken { get; set; }
    public string? AgentCertSha256 { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public string? XrayVersion { get; set; }
    public string? LastApplyError { get; set; }
    public DateTime? LastAppliedAt { get; set; }
    public bool IsOnline { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryUsedPercent { get; set; }
    public int ConsecutiveFailures { get; set; }
    public bool OfflineNotified { get; set; }
    public string? LastNotifiedApplyError { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
