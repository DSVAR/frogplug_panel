namespace FrogPlug.Server.Data;

public sealed class UserTrafficDaily
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }
    public long UpBytes { get; set; }
    public long DownBytes { get; set; }
}
