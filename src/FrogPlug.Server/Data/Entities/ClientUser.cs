using System.ComponentModel.DataAnnotations.Schema;

namespace FrogPlug.Server.Data;

public sealed class ClientUser
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public string? Note { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public long? TrafficLimitBytes { get; set; }
    public long UsedUploadBytes { get; set; }
    public long UsedDownloadBytes { get; set; }
    public Guid VlessUuid { get; set; } = Guid.NewGuid();
    public required string SubscriptionToken { get; set; }
    public bool AllowReality { get; set; } = true;
    public bool AllowXhttp { get; set; } = true;
    public bool AllowWhitelist { get; set; }
    public bool AllowMtproto { get; set; }
    public bool AllowTgWeb { get; set; }
    public bool AllowHysteria { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSubscriptionAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool ExpiredNotified { get; set; }
    public bool OverLimitNotified { get; set; }

    [NotMapped]
    public bool IsActive => IsActiveAt(DateTime.UtcNow);

    public bool IsActiveAt(DateTime nowUtc)
    {
        var notExpired = ExpiresAt is null || ExpiresAt > nowUtc;
        var withinLimit = TrafficLimitBytes is null || UsedUploadBytes + UsedDownloadBytes < TrafficLimitBytes;
        return IsEnabled && notExpired && withinLimit;
    }
}
