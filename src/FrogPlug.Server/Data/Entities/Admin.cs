namespace FrogPlug.Server.Data;

public sealed class Admin
{
    public int Id { get; set; }
    public required string Login { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? TotpSecret { get; set; }
    public long? LastTotpStep { get; set; }
    public bool IsActive { get; set; } = true;
}
