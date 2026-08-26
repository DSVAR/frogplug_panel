using System.Text.Json;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class NodeSyncServiceTests
{
    [Fact]
    public async Task Config_contains_only_active_users()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        AddUser(fixture, "active");
        AddUser(fixture, "disabled", enabled: false);
        AddUser(fixture, "expired", expires: fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1));
        AddUser(fixture, "limit", limit: 1, used: 1);
        var node = AddNode(fixture, "entry", NodeRole.Entry, "example.test");
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        await new NodeSyncService(fixture.Db, agent, fixture.Clock).SyncNode(node.Id);
        using var document = JsonDocument.Parse(agent.AppliedConfigs.Single().Config);
        var clients = document.RootElement.GetProperty("inbounds").EnumerateArray()
            .Single(item => item.GetProperty("tag").GetString() == "vless-reality")
            .GetProperty("settings").GetProperty("clients");
        Assert.Equal(["u1"], clients.EnumerateArray().Select(item => item.GetProperty("email").GetString()!).ToArray());
    }

    [Fact]
    public async Task Sync_applies_enabled_mtproto()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = AddNode(fixture, "entry", NodeRole.Entry, "node.example.com");
        node.MtprotoEnabled = true;
        node.MtprotoHost = "tg.example.com";
        node.MtprotoSecret = FrogPlug.Core.MtprotoSecret.Generate(node.MtprotoHost);
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();

        await new NodeSyncService(fixture.Db, agent, fixture.Clock, settings: fixture.Settings).SyncNode(node.Id);

        var request = Assert.Single(agent.AppliedMtproto).Request;
        Assert.True(request.Enabled);
        Assert.Equal(node.MtprotoSecret, request.Secret);
    }

    [Fact]
    public async Task Sync_skips_mtproto_without_own_host_but_applies_xray()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = AddNode(fixture, "entry", NodeRole.Entry, "node.example.com");
        node.MtprotoEnabled = true;
        node.MtprotoSecret = FrogPlug.Core.MtprotoSecret.Generate(node.Domain!);
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();

        await new NodeSyncService(fixture.Db, agent, fixture.Clock, settings: fixture.Settings).SyncNode(node.Id);

        Assert.False(Assert.Single(agent.AppliedMtproto).Request.Enabled);
        Assert.Single(agent.AppliedConfigs);
        Assert.Null(node.LastApplyError);
    }

    [Fact]
    public async Task Sync_tgweb_does_not_start_mtg_but_starts_tgweb()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = AddNode(fixture, "entry", NodeRole.Entry, "node.example.com");
        node.TgWebEnabled = true;
        node.MtprotoSecret = FrogPlug.Core.MtprotoSecret.Generate(node.Domain!);
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();

        await new NodeSyncService(fixture.Db, agent, fixture.Clock, settings: fixture.Settings).SyncNode(node.Id);

        Assert.False(Assert.Single(agent.AppliedMtproto).Request.Enabled);
        var request = Assert.Single(agent.AppliedTgWeb).Request;
        Assert.True(request.Enabled);
        Assert.Equal(NodePorts.TelegramWebMtproxy, request.BackendPort);
    }

    [Fact]
    public async Task Apply_failure_sets_error_and_success_sets_timestamp()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = AddNode(fixture, "node", NodeRole.Exit, null);
        var old = fixture.Clock.GetUtcNow().UtcDateTime.AddHours(-1);
        node.LastAppliedAt = old;
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        agent.ThrowOnApplyXray.Add(node.Name);
        await new NodeSyncService(fixture.Db, agent, fixture.Clock).SyncNode(node.Id);
        Assert.Equal("apply failure", node.LastApplyError);
        Assert.Equal(old, node.LastAppliedAt);
        agent.ThrowOnApplyXray.Clear();
        await new NodeSyncService(fixture.Db, agent, fixture.Clock).SyncNode(node.Id);
        Assert.Null(node.LastApplyError);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, node.LastAppliedAt);
    }

    [Fact]
    public async Task Caddy_is_applied_for_each_own_domain()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var entry = AddNode(fixture, "entry", NodeRole.Entry, "entry.test");
        AddNode(fixture, "entry-no-domain", NodeRole.Entry, null);
        AddNode(fixture, "exit-domain", NodeRole.Exit, "exit.test");
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        var service = new NodeSyncService(fixture.Db, agent, fixture.Clock);
        await service.SyncNode(entry.Id);
        await service.SyncNode(entry.Id + 1);
        await service.SyncNode(entry.Id + 2);
        Assert.Equal(["entry", "exit-domain"], agent.AppliedCaddy.Select(item => item.Node));
    }

    private static ClientUser AddUser(ServiceFixture fixture, string name, bool enabled = true, DateTime? expires = null, long? limit = null, long used = 0)
    {
        var user = new ClientUser {
            Name = name,
            SubscriptionToken = $"token-{name}",
            IsEnabled = enabled,
            ExpiresAt = expires,
            TrafficLimitBytes = limit,
            UsedUploadBytes = used,
            CreatedAt = fixture.Clock.GetUtcNow().UtcDateTime,
        };
        fixture.Db.ClientUsers.Add(user);
        return user;
    }

    private static Node AddNode(ServiceFixture fixture, string name, NodeRole role, string? domain)
    {
        var keys = FrogPlug.Core.RealityKeys.Generate();
        var node = new Node
        {
            Name = name, Address = "127.0.0.1", Country = "RU", Role = role, Domain = domain,
            RealityPrivateKey = keys.PrivateKey,
            RealityPublicKey = keys.PublicKey,
            RealityShortId = "abcdef12",
            XhttpPath = "/abcdef123456",
            AgentUrl = "https://localhost",
            AgentToken = "token",
            AgentCertSha256 = "cert",
            MaskMode = string.IsNullOrWhiteSpace(domain) ? MaskMode.ForeignSite : MaskMode.OwnDomain,
            RealitySni = string.IsNullOrWhiteSpace(domain) ? "mask.example" : null,
        };
        fixture.Db.Nodes.Add(node);
        return node;
    }
}
