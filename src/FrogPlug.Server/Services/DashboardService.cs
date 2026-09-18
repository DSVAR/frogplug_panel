using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed class DashboardService(AppDbContext db, TimeProvider time)
{
    public async Task<DashboardData> Get(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var users = await db.ClientUsers.AsNoTracking().ToListAsync(cancellationToken);
        var nodes = await db.Nodes.AsNoTracking().ToListAsync(cancellationToken);
        var traffic = await GetTraffic(now, cancellationToken);
        return new DashboardData(
            new UserDashboardData(users.Count, users.Count(item => item.CreatedAt >= now.AddHours(-24))),
            GetNodes(nodes),
            users.Count(item => item.LastActivityAt >= now.AddMinutes(-5)),
            traffic,
            GetExpiring(users, now),
            GetCpu(nodes),
            await GetAudit(cancellationToken));
    }

    private async Task<TrafficDashboardData> GetTraffic(DateTime now, CancellationToken cancellationToken)
    {
        var hour = HourStart(now);
        var start = hour.AddHours(-23);
        var rows = await db.TrafficHourly.AsNoTracking()
            .Where(item => item.Date >= start && item.Date <= hour)
            .ToListAsync(cancellationToken);
        var points = Enumerable.Range(0, 24).Select(index => CreatePoint(rows, start.AddHours(index))).ToArray();
        return new TrafficDashboardData(points.Sum(item => item.UpBytes), points.Sum(item => item.DownBytes), points);
    }

    private static TrafficHourPoint CreatePoint(IEnumerable<TrafficHourly> rows, DateTime hour)
    {
        var row = rows.FirstOrDefault(item => item.Date == hour);
        return new TrafficHourPoint(hour, row?.UpBytes ?? 0, row?.DownBytes ?? 0);
    }

    private static NodeDashboardData GetNodes(IEnumerable<Node> nodes)
    {
        var list = nodes.ToArray();
        return new NodeDashboardData(
            list.Length,
            list.Count(item => item.IsOnline),
            list.Count(item => item.IsOnline && (item.CpuPercent > 80 || item.LastApplyError is not null)),
            list.Count(item => !item.IsOnline));
    }

    private static ExpiringDashboardData GetExpiring(IEnumerable<ClientUser> users, DateTime now)
    {
        var items = users.Where(item => item.ExpiresAt > now && item.ExpiresAt <= now.AddDays(7))
            .OrderBy(item => item.ExpiresAt)
            .Select(item => new ExpiringUser(item.Name, item.Note, item.ExpiresAt!.Value))
            .ToArray();
        return new ExpiringDashboardData(items.Length, items.Take(5).ToArray());
    }

    private static IReadOnlyList<NodeCpuData> GetCpu(IEnumerable<Node> nodes)
    {
        return nodes.OrderByDescending(item => item.CpuPercent)
            .Select(item => new NodeCpuData(item.Name, item.CpuPercent, item.IsOnline))
            .ToArray();
    }

    private async Task<IReadOnlyList<AuditDashboardItem>> GetAudit(CancellationToken cancellationToken)
    {
        var entries = await db.AuditLogs.AsNoTracking().OrderByDescending(item => item.At).Take(5)
            .ToListAsync(cancellationToken);
        return entries.Select(item => new AuditDashboardItem(item.At, AuditLabels.Get(item.Action), item.AdminLogin)).ToArray();
    }

    private static DateTime HourStart(DateTime value)
    {
        return new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, DateTimeKind.Utc);
    }
}

public sealed record DashboardData(
    UserDashboardData Users,
    NodeDashboardData Nodes,
    int OnlineNow,
    TrafficDashboardData Traffic,
    ExpiringDashboardData Expiring,
    IReadOnlyList<NodeCpuData> NodeCpu,
    IReadOnlyList<AuditDashboardItem> Audit);

public sealed record UserDashboardData(int Total, int AddedLast24Hours);
public sealed record NodeDashboardData(int Total, int Online, int Warning, int Offline);
public sealed record TrafficDashboardData(long UpBytes, long DownBytes, IReadOnlyList<TrafficHourPoint> Points);
public sealed record TrafficHourPoint(DateTime Hour, long UpBytes, long DownBytes);
public sealed record ExpiringDashboardData(int Total, IReadOnlyList<ExpiringUser> Items);
public sealed record ExpiringUser(string Name, string? Note, DateTime ExpiresAt);
public sealed record NodeCpuData(string Name, double CpuPercent, bool IsOnline);
public sealed record AuditDashboardItem(DateTime At, string Label, string Actor);
