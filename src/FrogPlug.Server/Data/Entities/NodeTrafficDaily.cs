namespace FrogPlug.Server.Data;

public sealed class NodeTrafficDaily
{
    public int Id { get; set; }
    public int NodeId { get; set; }
    public DateTime Date { get; set; }
    public long UpBytes { get; set; }
    public long DownBytes { get; set; }
}
