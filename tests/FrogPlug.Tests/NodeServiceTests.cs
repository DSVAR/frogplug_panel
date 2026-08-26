using FrogPlug.Server.Services;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using FrogPlug.Server.Data;

namespace FrogPlug.Tests;

public sealed class NodeServiceTests
{
    [Fact]
    public async Task Create_generates_all_secrets_and_paths()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput("node", "example.com", "RU", NodeRole.Entry));
        Assert.Equal(43, node.RealityPrivateKey.Length);
        Assert.Equal(43, node.RealityPublicKey.Length);
        Assert.Matches("^[0-9a-f]{8}$", node.RealityShortId);
        Assert.Matches("^/[0-9a-f]{12}$", node.XhttpPath);
        Assert.Equal(43, node.AgentToken.Length);
        Assert.NotEqual(Guid.Empty, node.CascadeUuid);
    }

    [Fact]
    public async Task Invalid_country_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Nodes.Create(new NodeInput("node", "example.com", "RUS", NodeRole.Entry)));
    }

    [Fact]
    public async Task Equal_ports_are_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Nodes.Create(new NodeInput("node", "example.com", "RU", NodeRole.Entry, XhttpPort: 8443)));
    }

    [Fact]
    public async Task Delete_removes_node_targeted_rules()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput("node", "example.com", "RU", NodeRole.Entry));
        await fixture.Routing.Add(new RoutingRuleInput(RoutingMatchType.Domain, "example.com", $"node:{node.Id}"));
        await fixture.Nodes.Delete(node.Id);
        Assert.Empty(await fixture.Db.RoutingRules.ToListAsync());
    }

    [Fact]
    public async Task RegenerateAgentToken_changes_token()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput("node", "example.com", "RU", NodeRole.Entry));
        var old = node.AgentToken;
        await fixture.Nodes.RegenerateAgentToken(node.Id);
        Assert.NotEqual(old, node.AgentToken);
    }

    [Fact]
    public async Task Enable_mtproto_generates_secret_from_mtproto_host()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(
            new NodeInput("node", "example.com", "RU", NodeRole.Entry, Domain: "node.example.com", MtprotoHost: "tg.example.com"));

        await fixture.Nodes.EnableMtproto(node.Id);

        Assert.True(node.MtprotoEnabled);
        Assert.Equal("tg.example.com", FrogPlug.Core.MtprotoSecret.Parse(node.MtprotoSecret!));
    }

    [Fact]
    public async Task Enable_mtproto_on_node_domain_is_rejected()
    {
        // На домене ноды уже стоит Reality: по одинаковому SNI HAProxy не отличит MTProto.
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(
            new NodeInput("node", "example.com", "RU", NodeRole.Entry, Domain: "node.example.com", MtprotoHost: "node.example.com"));

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Nodes.EnableMtproto(node.Id));
    }

    [Fact]
    public async Task Changing_mtproto_host_regenerates_secret()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var input = new NodeInput("node", "example.com", "RU", NodeRole.Entry, Domain: "node.example.com", MtprotoHost: "tg.example.com");
        var node = await fixture.Nodes.Create(input);
        await fixture.Nodes.EnableMtproto(node.Id);

        await fixture.Nodes.Update(node.Id, input with { MtprotoHost = "mt.example.com" });

        Assert.Equal("mt.example.com", FrogPlug.Core.MtprotoSecret.Parse(node.MtprotoSecret!));
    }

    [Fact]
    public async Task Enable_mtproto_without_domain_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput("node", "example.com", "RU", NodeRole.Entry));

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Nodes.EnableMtproto(node.Id));
    }

    [Fact]
    public async Task Protocol_flags_persist_false_after_reload()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var input = new NodeInput(
            "node",
            "example.com",
            "RU",
            NodeRole.Entry,
            RealityEnabled: false,
            XhttpEnabled: false);
        var node = await fixture.Nodes.Create(input);

        fixture.Db.ChangeTracker.Clear();
        var reloaded = await fixture.Db.Nodes.SingleAsync(item => item.Id == node.Id);

        Assert.False(reloaded.RealityEnabled);
        Assert.False(reloaded.XhttpEnabled);
    }
}


