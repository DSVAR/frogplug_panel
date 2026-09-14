using FrogPlug.Server.Services;
using System.ComponentModel.DataAnnotations;
using FrogPlug.Server.Data;

namespace FrogPlug.Tests;

public sealed class RoutingServiceTests
{
    [Fact]
    public async Task Seed_creates_seven_rules_only_once()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await fixture.Routing.SeedDefaultsAsync();
        await fixture.Routing.SeedDefaultsAsync();
        var rules = await fixture.Routing.List();
        Assert.Equal(7, rules.Count);
        Assert.Equal("block", rules[0].Target);
    }

    [Fact]
    public async Task MoveUp_and_down_keep_dense_order()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await fixture.Routing.SeedDefaultsAsync();
        var rules = await fixture.Routing.List();
        await fixture.Routing.MoveDown(rules[0].Id);
        await fixture.Routing.MoveUp(rules[0].Id);
        var orders = (await fixture.Routing.List()).Select(rule => rule.Order).ToArray();
        Assert.Equal(Enumerable.Range(1, 7), orders);
    }

    [Fact]
    public async Task Invalid_target_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Routing.Add(new RoutingRuleInput(RoutingMatchType.Domain, "example.com", "invalid")));
    }

    [Fact]
    public async Task Missing_node_target_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Routing.Add(new RoutingRuleInput(RoutingMatchType.Domain, "example.com", "node:123")));
    }

    [Fact]
    public async Task Existing_node_target_is_accepted()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput("node", "example.com", "RU", NodeRole.Exit));
        var rule = await fixture.Routing.Add(new RoutingRuleInput(RoutingMatchType.Domain, "example.com", $"node:{node.Id}"));
        Assert.Equal($"node:{node.Id}", rule.Target);
    }
}

