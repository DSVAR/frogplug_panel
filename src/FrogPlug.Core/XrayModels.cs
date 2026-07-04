namespace FrogPlug.Core;

public sealed record NodeSpec
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string Roles { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public string? Domain { get; init; }
    public bool RealityEnabled { get; init; } = true;
    public int RealityPort { get; init; } = NodePorts.Reality;
    public MaskMode MaskMode { get; init; } = MaskMode.OwnDomain;
    public string RealitySni { get; init; } = string.Empty;
    public string RealityFingerprint { get; init; } = "chrome";
    public int? WhitelistExitNodeId { get; init; }
    public string RealityPrivateKey { get; init; } = string.Empty;
    public string RealityPublicKey { get; init; } = string.Empty;
    public string RealityShortId { get; init; } = string.Empty;
    public string RealityServerName { get; init; } = string.Empty;
    public string RealityDest { get; init; } = string.Empty;
    public string XhttpPath { get; init; } = string.Empty;
    public string XhttpMode { get; init; } = "auto";
    public bool XhttpEnabled { get; init; } = true;
    public string? MtprotoHost { get; init; }
    public Guid CascadeUuid { get; init; }

    public bool HasRole(string role) => Roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Any(item => string.Equals(item, role, StringComparison.OrdinalIgnoreCase));
}

public sealed record UserSpec
{
    public int Id { get; init; }
    public Guid VlessUuid { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsEnabled { get; init; } = true;
    public bool AllowReality { get; init; } = true;
    public bool AllowXhttp { get; init; } = true;
    public bool AllowWhitelist { get; init; }
    public bool AllowTgWeb { get; init; }
    public bool AllowHysteria { get; init; }
}

public sealed record RuleSpec
{
    public int Order { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Target { get; init; } = "direct";
    public bool IsEnabled { get; init; } = true;
}

public sealed record XrayBuildOptions
{
    public bool IncludePrivateBlock { get; init; } = true;
}

