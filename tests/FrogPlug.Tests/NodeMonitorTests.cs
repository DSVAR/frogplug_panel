using FrogPlug.Core;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Tests;

public sealed class NodeMonitorTests
{
    [Fact]
    public async Task Collect_updates_user_traffic_and_activity()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user1 = AddUser(fixture, "one");
        var user2 = AddUser(fixture, "two");
        var node = AddNode(fixture, "node1");
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        agent.Stats[node.Name] = new TrafficStats(new Dictionary<string, TrafficStat>
        {
            ["u1"] = new(100, 200), ["u2"] = new(0, 0), ["node2"] = new(5, 5)
        });
        await new NodeMonitor(fixture.Db, agent, fixture.Clock).CollectOnce();
        Assert.Equal(100, user1.UsedUploadBytes);
        Assert.Equal(200, user1.UsedDownloadBytes);
        Assert.Equal(0, user2.UsedUploadBytes + user2.UsedDownloadBytes);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, user1.LastActivityAt);
        Assert.Null(user2.LastActivityAt);
    }

    [Fact]
    public async Task Unknown_and_node_emails_are_ignored()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user = AddUser(fixture, "one");
        var node = AddNode(fixture, "node1");
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        agent.Stats[node.Name] = new TrafficStats(new Dictionary<string, TrafficStat>
        {
            ["node2"] = new(10, 10), ["u999"] = new(20, 20)
        });
        await new NodeMonitor(fixture.Db, agent, fixture.Clock).CollectOnce();
        Assert.Equal(0, user.UsedUploadBytes + user.UsedDownloadBytes);
        var daily = await fixture.Db.NodeTrafficDaily.SingleAsync();
        Assert.Equal(0, daily.UpBytes + daily.DownBytes);
    }

    [Fact]
    public async Task Daily_rows_accumulate_and_roll_over_by_date()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user = AddUser(fixture, "one");
        var node = AddNode(fixture, "node1");
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        agent.Stats[node.Name] = new TrafficStats(new Dictionary<string, TrafficStat> { ["u1"] = new(1, 2) });
        var monitor = new NodeMonitor(fixture.Db, agent, fixture.Clock);
        await monitor.CollectOnce();
        await monitor.CollectOnce();
        fixture.Clock.SetUtcNow(fixture.Clock.GetUtcNow().AddDays(1));
        await monitor.CollectOnce();
        var rows = await fixture.Db.NodeTrafficDaily.OrderBy(item => item.Date).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal((long)2, rows[0].UpBytes);
        Assert.Equal((long)4, rows[0].DownBytes);
        Assert.Equal((long)1, rows[1].UpBytes);
        Assert.Equal((long)2, rows[1].DownBytes);
        Assert.Equal(3, user.UsedUploadBytes);
        var userRows = await fixture.Db.UserTrafficDaily.OrderBy(item => item.Date).ToListAsync();
        Assert.Equal(2, userRows.Count);
        Assert.Equal((long)2, userRows[0].UpBytes);
        Assert.Equal((long)4, userRows[0].DownBytes);
        Assert.Equal((long)1, userRows[1].UpBytes);
        var hourlyRows = await fixture.Db.TrafficHourly.OrderBy(item => item.Date).ToListAsync();
        Assert.Equal(2, hourlyRows.Count);
        Assert.Equal((long)2, hourlyRows[0].UpBytes);
        Assert.Equal((long)4, hourlyRows[0].DownBytes);
    }

    [Fact]
    public async Task Status_is_saved_and_marks_node_online()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = AddNode(fixture, "node1");
        var old = fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-1);
        node.LastSeenAt = old;
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        agent.Statuses[node.Name] = Status("1.2.3", 12.5, 34.5);
        await new NodeMonitor(fixture.Db, agent, fixture.Clock).CollectOnce();
        Assert.True(node.IsOnline);
        Assert.Equal("1.2.3", node.XrayVersion);
        Assert.Equal(12.5, node.CpuPercent);
        Assert.Equal(34.5, node.MemoryUsedPercent);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, node.LastSeenAt);
    }

    [Fact]
    public async Task Failed_node_does_not_stop_other_nodes()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var first = AddNode(fixture, "A");
        var second = AddNode(fixture, "B");
        first.LastSeenAt = fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-1);
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        agent.ThrowOnStatus.Add(first.Name);
        agent.Statuses[second.Name] = Status("ok", 1, 2);
        await new NodeMonitor(fixture.Db, agent, fixture.Clock).CollectOnce();
        Assert.False(first.IsOnline);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-1), first.LastSeenAt);
        Assert.True(second.IsOnline);
    }

    [Fact]
    public async Task Connected_transport_does_not_require_certificate_configuration()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var connected = AddNode(fixture, "missing", cert: null);
        AddNode(fixture, "disabled", enabled: false);
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        await new NodeMonitor(fixture.Db, agent, fixture.Clock).CollectOnce();
        Assert.Equal([connected.Name], agent.StatusCalls);
        Assert.Equal([connected.Name], agent.StatsCalls);
    }

    private static ClientUser AddUser(ServiceFixture fixture, string name)
    {
        var user = new ClientUser { Name = name, SubscriptionToken = $"token-{name}", CreatedAt = fixture.Clock.GetUtcNow().UtcDateTime };
        fixture.Db.ClientUsers.Add(user);
        return user;
    }

    private static Node AddNode(ServiceFixture fixture, string name, bool enabled = true, string? cert = "cert")
    {
        var keys = RealityKeys.Generate();
        var node = new Node
        {
            Name = name, Address = "127.0.0.1", Country = "RU", Role = NodeRole.Entry, IsEnabled = enabled,
            RealityPrivateKey = keys.PrivateKey,
            RealityPublicKey = keys.PublicKey,
            RealityShortId = "abcdef12",
            AgentUrl = "https://localhost",
            AgentToken = "token",
            AgentCertSha256 = cert,
        };
        fixture.Db.Nodes.Add(node);
        return node;
    }

    private static AgentStatus Status(string version, double cpu, double memory)
    {
        var component = new ComponentStatus(true, true, version);
        return new AgentStatus(version, "host", 1, 0, 1, 1, component, component, component, new OpenFluxStatus(false, []), cpu, memory);
    }
}
