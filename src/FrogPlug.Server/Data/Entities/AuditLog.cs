namespace FrogPlug.Server.Data;

public sealed class AuditLog
{
    public int Id { get; set; }
    public DateTime At { get; set; }
    public required string AdminLogin { get; set; }
    public required string Action { get; set; }
    public string? Details { get; set; }
}
