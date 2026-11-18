namespace FrogPlug.Server.Data;

public enum RoutingMatchType
{
    Geosite,
    Geoip,
    Domain,
    Full,
    Ip,
    Port,
    Protocol
}

public sealed class RoutingRule
{
    public int Id { get; set; }
    public int Order { get; set; }
    public RoutingMatchType MatchType { get; set; }
    public required string Value { get; set; }
    public required string Target { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Comment { get; set; }
}
