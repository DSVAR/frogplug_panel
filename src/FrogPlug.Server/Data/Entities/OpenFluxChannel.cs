namespace FrogPlug.Server.Data;

public sealed class OpenFluxChannel
{
    public int Id { get; set; }
    public int NodeId { get; set; }
    public required string Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Mode { get; set; } = "l4";
    public int Port { get; set; }
    public required string Key { get; set; }
    public string? VyandexUrl { get; set; }
    public string? YandexUrl { get; set; }
    public string? MailruUrl { get; set; }
    public string? TelemostUrl { get; set; }
    public string? BoardsUrl { get; set; }
    public string? AvitoUrl { get; set; }
    public string? CupsUrl { get; set; }
    public string? MaxToken { get; set; }
    public long? MaxUid { get; set; }
    public string? CookiesJson { get; set; }
    public bool UseXrayUpstream { get; set; } = true;
    public string? LastApplyError { get; set; }
    public DateTime? LastAppliedAt { get; set; }
    public Node? Node { get; set; }
}
