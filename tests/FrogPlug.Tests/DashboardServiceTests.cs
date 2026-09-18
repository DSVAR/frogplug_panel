using FrogPlug.Server.Data;
using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task Returns_all_dashboard_numbers_from_seeded_database()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        fixture.Db.ClientUsers.AddRange(
            new ClientUser { Name = "new", SubscriptionToken = "new", CreatedAt = now.AddHours(-1), LastActivityAt = now.AddMinutes(-1) },
            new ClientUser { Name = "old", SubscriptionToken = "old", CreatedAt = now.AddDays(-2) },
            new ClientUser { Name = "soon", SubscriptionToken = "soon", ExpiresAt = now.AddDays(1), Note = "note" });
        fixture.Db.Nodes.AddRange(Node("online", true, 10), Node("warning", true, 81), Node("offline", false, 0));
        fixture.Db.TrafficHourly.Add(new TrafficHourly { Date = now, UpBytes = 10, DownBytes = 20 });
        fixture.Db.AuditLogs.Add(new AuditLog { At = now, Action = AuditActions.NodeCreate, AdminLogin = "admin" });
        await fixture.Db.SaveChangesAsync();
        var data = await new DashboardService(fixture.Db, fixture.Clock).Get();
        Assert.Equal(3, data.Users.Total);
        Assert.Equal(1, data.Users.AddedLast24Hours);
        Assert.Equal(3, data.Nodes.Total);
        Assert.Equal(2, data.Nodes.Online);
        Assert.Equal(1, data.Nodes.Warning);
        Assert.Equal(1, data.Nodes.Offline);
        Assert.Equal(1, data.OnlineNow);
        Assert.Equal(10, data.Traffic.UpBytes);
        Assert.Equal(20, data.Traffic.DownBytes);
        Assert.Equal(24, data.Traffic.Points.Count);
        Assert.Equal(1, data.Expiring.Total);
        Assert.Equal("soon", data.Expiring.Items.Single().Name);
        Assert.Equal(3, data.NodeCpu.Count);
        Assert.Equal("Создана нода", data.Audit.Single().Label);
        Assert.Equal("admin", data.Audit.Single().Actor);
    }

    private static Node Node(string name, bool online, double cpu)
    {
        return new Node
        {
            Name = name, Address = "127.0.0.1", Country = "RU", RealityPrivateKey = "private",
            RealityPublicKey = "public", RealityShortId = "12345678", AgentUrl = "https://localhost",
            AgentToken = "token", IsOnline = online, CpuPercent = cpu
        };
    }
}
