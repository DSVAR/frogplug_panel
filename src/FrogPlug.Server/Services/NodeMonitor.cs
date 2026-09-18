using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

/// <summary>
/// Опрашивает агентов нод: состояние, метрики и трафик пользователей (scoped, на один проход).
/// </summary>
public sealed class NodeMonitor(
    AppDbContext db,
    IAgentClient agent,
    TimeProvider time,
    ITelegramNotifier? notifier = null)
{
    public async Task CollectOnce(CancellationToken cancellationToken = default)
    {
        var nodes = await db.Nodes.Where(item => item.IsEnabled)
            .ToListAsync(cancellationToken);
        foreach (var node in nodes)
        {
            await CollectNode(node, cancellationToken);
        }

        await NotifyUserTransitions(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task CollectNode(Node node, CancellationToken cancellationToken)
    {
        try
        {
            var status = await agent.GetStatus(node, cancellationToken);
            node.IsOnline = status is not null;
            if (status is not null)
            {
                node.LastSeenAt = UtcNow;
                node.XrayVersion = status.Xray.Version ?? node.XrayVersion;
                node.CpuPercent = status.CpuPercent;
                node.MemoryUsedPercent = status.MemoryUsedPercent;
            }

            var stats = await agent.GetStats(node, true, cancellationToken);
            if (stats is not null)
            {
                await ApplyStats(node, stats);
            }

            if (status is not null || stats is not null)
            {
                await RegisterSuccess(node, cancellationToken);
            }
            else
            {
                await RegisterFailure(node, cancellationToken);
            }
        }
        catch
        {
            node.IsOnline = false;
            await RegisterFailure(node, cancellationToken);
        }
    }

    private async Task ApplyStats(Node node, TrafficStats stats)
    {
        var date = UtcNow.Date;
        var daily = await db.NodeTrafficDaily.FirstOrDefaultAsync(item => item.NodeId == node.Id && item.Date == date);
        if (daily is null)
        {
            daily = new NodeTrafficDaily { NodeId = node.Id, Date = date };
            db.NodeTrafficDaily.Add(daily);
        }

        var hour = new DateTime(UtcNow.Year, UtcNow.Month, UtcNow.Day, UtcNow.Hour, 0, 0, DateTimeKind.Utc);
        var hourly = await db.TrafficHourly.FirstOrDefaultAsync(item => item.Date == hour);
        if (hourly is null)
        {
            hourly = new TrafficHourly { Date = hour };
            db.TrafficHourly.Add(hourly);
        }

        foreach (var pair in stats.Users)
        {
            if (!pair.Key.StartsWith("u", StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(pair.Key[1..], out var userId))
            {
                continue;
            }

            var user = await db.ClientUsers.FirstOrDefaultAsync(item => item.Id == userId);
            if (user is null)
            {
                continue;
            }

            var userDaily = await db.UserTrafficDaily.FirstOrDefaultAsync(
                item => item.UserId == userId && item.Date == date);
            if (userDaily is null)
            {
                userDaily = new UserTrafficDaily { UserId = userId, Date = date };
                db.UserTrafficDaily.Add(userDaily);
            }

            user.UsedUploadBytes += pair.Value.Up;
            user.UsedDownloadBytes += pair.Value.Down;
            if (pair.Value.Up > 0 || pair.Value.Down > 0)
            {
                user.LastActivityAt = UtcNow;
            }

            daily.UpBytes += pair.Value.Up;
            daily.DownBytes += pair.Value.Down;
            userDaily.UpBytes += pair.Value.Up;
            userDaily.DownBytes += pair.Value.Down;
            hourly.UpBytes += pair.Value.Up;
            hourly.DownBytes += pair.Value.Down;
        }

        var cutoff = UtcNow.AddDays(-7);
        var oldRows = await db.TrafficHourly.Where(item => item.Date < cutoff).ToListAsync();
        db.TrafficHourly.RemoveRange(oldRows);
    }

    private async Task RegisterFailure(Node node, CancellationToken cancellationToken)
    {
        node.ConsecutiveFailures++;
        if (node.ConsecutiveFailures < 3 || node.OfflineNotified)
        {
            return;
        }

        node.OfflineNotified = true;
        if (notifier is not null)
        {
            await notifier.Send($"Нода недоступна: {node.Name}", cancellationToken);
        }
    }

    private async Task RegisterSuccess(Node node, CancellationToken cancellationToken)
    {
        var wasOffline = node.OfflineNotified;
        node.ConsecutiveFailures = 0;
        node.OfflineNotified = false;
        if (wasOffline && notifier is not null)
        {
            await notifier.Send($"Нода снова доступна: {node.Name}", cancellationToken);
        }
    }

    private async Task NotifyUserTransitions(CancellationToken cancellationToken)
    {
        var localNow = time.GetLocalNow();
        if (localNow.Hour < 10 || notifier is null)
        {
            return;
        }

        var users = await db.ClientUsers.ToListAsync(cancellationToken);
        var newTransitions = new List<string>();
        foreach (var user in users)
        {
            var status = ClientUserService.GetStatus(user, UtcNow);
            if (status == ClientStatus.Expired && !user.ExpiredNotified)
            {
                user.ExpiredNotified = true;
                newTransitions.Add($"{user.Name}: срок истёк");
            }
            else if (status == ClientStatus.OverLimit && !user.OverLimitNotified)
            {
                user.OverLimitNotified = true;
                newTransitions.Add($"{user.Name}: лимит исчерпан");
            }
            else if (status == ClientStatus.Active)
            {
                user.ExpiredNotified = false;
                user.OverLimitNotified = false;
            }
        }

        if (newTransitions.Count > 0)
        {
            var list = string.Join("\n", newTransitions.Take(20));
            await notifier.Send($"Пользователи с новым ограничением ({newTransitions.Count}):\n{list}", cancellationToken);
        }
    }

    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
}
