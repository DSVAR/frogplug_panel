using System.Text.Json.Nodes;
using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class XrayConfigBuilderTests
{
    private static NodeSpec Node(int id, string roles = "Entry", bool enabled = true) => new()
    {
        Id = id,
        Name = $"N{id}",
        Address = $"198.51.100.{id}",
        Country = "CZ",
        Roles = roles,
        IsEnabled = enabled,
        RealityServerName = "example.com",
        RealityDest = "example.com:443",
        RealityPrivateKey = "private",
        RealityPublicKey = "public",
        RealityShortId = "sid",
        CascadeUuid = Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}")
    };

    private static JsonObject Config(NodeSpec self, IReadOnlyList<NodeSpec> nodes, IReadOnlyList<RuleSpec> rules)
    {
        return JsonNode.Parse(XrayConfigBuilder.Build(self, nodes, [], rules))!.AsObject();
    }

    private static JsonArray RoutingRules(JsonObject config)
    {
        return config["routing"]!["rules"]!.AsArray();
    }

    [Fact]
    public void No_api_outbound()
    {
        var config = Config(Node(1), [Node(1)], []);
        var outbounds = config["outbounds"]!.AsArray();
        Assert.DoesNotContain(outbounds, item => (string?)item!["tag"] == "api");
    }

    [Fact]
    public void Api_rule_targets_api_handler()
    {
        var routingRules = RoutingRules(Config(Node(1), [Node(1)], []));

        Assert.Equal("api", (string?)routingRules[0]!["inboundTag"]![0]);
        Assert.Equal("api", (string?)routingRules[0]!["outboundTag"]);
    }

    [Fact]
    public void Self_role_target_is_direct()
    {
        var rules = new[] { new RuleSpec { Kind = "Domain", Value = "x", Target = "role:Entry" } };
        var routingRules = RoutingRules(Config(Node(1), [Node(1)], rules));
        Assert.Equal("direct", (string?)routingRules[2]!["outboundTag"]);
    }

    [Fact]
    public void Missing_role_target_is_direct()
    {
        var rules = new[] { new RuleSpec { Kind = "Domain", Value = "x", Target = "role:Exit" } };
        var routingRules = RoutingRules(Config(Node(1), [Node(1)], rules));
        Assert.Equal("direct", (string?)routingRules[2]!["outboundTag"]);
    }

    [Fact]
    public void Disabled_node_has_no_outbound()
    {
        var rules = new[] { new RuleSpec { Kind = "Domain", Value = "x", Target = "node:2" } };
        var config = Config(Node(1), [Node(1), Node(2, enabled: false)], rules);
        Assert.DoesNotContain(config["outbounds"]!.AsArray(), item => (string?)item!["tag"] == "node-2");
    }

    [Fact]
    public void Xhttp_requires_domain()
    {
        var config = Config(Node(1), [Node(1)], []);
        Assert.DoesNotContain(config["inbounds"]!.AsArray(), item => (string?)item!["tag"] == "vless-xhttp");
    }

    [Fact]
    public void Xhttp_with_domain_is_present()
    {
        var node = Node(1) with { Domain = "vpn.example" };
        var config = Config(node, [node], []);
        Assert.Contains(config["inbounds"]!.AsArray(), item => (string?)item!["tag"] == "vless-xhttp");
    }

    [Fact]
    public void Cascade_rule_precedes_regular_rule()
    {
        var rules = new[]
        {
            new RuleSpec { Order = 1, Kind = "Domain", Value = "ads", Target = "block" },
            new RuleSpec { Order = 2, Kind = "Domain", Value = "x", Target = "node:2" }
        };
        var config = Config(Node(1), [Node(1), Node(2)], rules);
        var routingRules = RoutingRules(config);
        Assert.Equal("api", (string?)routingRules[0]!["outboundTag"]);
        Assert.Equal("block", (string?)routingRules[1]!["outboundTag"]);
        Assert.Equal("block", (string?)routingRules[2]!["outboundTag"]);
        Assert.Equal("node-2", (string?)routingRules[3]!["outboundTag"]);
        Assert.Equal("domain:x", (string?)routingRules[3]!["domain"]![0]);
    }

    [Fact]
    public void Disabled_users_are_not_in_inbound_clients()
    {
        var active = new UserSpec { Id = 1, VlessUuid = Guid.NewGuid() };
        var disabled = new UserSpec { Id = 2, VlessUuid = Guid.NewGuid(), IsEnabled = false };
        var config = JsonNode.Parse(XrayConfigBuilder.Build(Node(1), [Node(1)], [active, disabled], []))!.AsObject();
        var clients = config["inbounds"]![1]!["settings"]!["clients"]!.AsArray();
        Assert.Contains(clients, item => (string?)item!["email"] == "u1");
        Assert.DoesNotContain(clients, item => (string?)item!["email"] == "u2");
    }

    [Fact]
    public void Users_are_added_only_to_allowed_inbounds()
    {
        var self = Node(1) with { Domain = "cz.example.com", XhttpPath = "/x" };
        UserSpec[] users =
        [
            new() { Id = 1, VlessUuid = Guid.NewGuid(), AllowReality = true, AllowXhttp = false },
            new() { Id = 2, VlessUuid = Guid.NewGuid(), AllowReality = false, AllowXhttp = true }
        ];

        var json = XrayConfigBuilder.Build(self, [self], users, []);
        var inbounds = JsonNode.Parse(json)!["inbounds"]!.AsArray();

        Assert.Equal(["u1"], Emails(inbounds, "vless-reality"));
        Assert.Equal(["u2"], Emails(inbounds, "vless-xhttp"));
    }

    [Fact]
    public void Reality_off_omits_user_clients()
    {
        var self = Node(1) with { RealityEnabled = false };
        var user = new UserSpec { Id = 1, VlessUuid = Guid.NewGuid() };

        var inbounds = JsonNode.Parse(XrayConfigBuilder.Build(self, [self], [user], []))!["inbounds"]!.AsArray();

        Assert.DoesNotContain(inbounds, item => (string?)item!["tag"] == "vless-reality");
    }

    [Fact]
    public void Reality_off_keeps_cascade_inbound_without_user_clients()
    {
        var exit = Node(1, "Exit") with { RealityEnabled = false };
        var entry = Node(2);
        var user = new UserSpec { Id = 1, VlessUuid = Guid.NewGuid() };
        var rules = new[] { new RuleSpec { Kind = "Domain", Value = "example.com", Target = "node:1" } };

        var inbounds = JsonNode.Parse(XrayConfigBuilder.Build(exit, [exit, entry], [user], rules))!["inbounds"]!.AsArray();

        Assert.Equal(["node2"], Emails(inbounds, "vless-reality"));
    }

    [Fact]
    public void Xhttp_off_omits_inbound()
    {
        var self = Node(1) with { Domain = "cz.example.com", XhttpEnabled = false };

        var inbounds = JsonNode.Parse(XrayConfigBuilder.Build(self, [self], [], []))!["inbounds"]!.AsArray();

        Assert.DoesNotContain(inbounds, item => (string?)item!["tag"] == "vless-xhttp");
    }

    [Fact]
    public void Whitelist_entry_routes_users_to_its_exit()
    {
        var entry = Node(1, "Entry, WhitelistEntry") with { WhitelistExitNodeId = 2 };
        var exit = Node(2, "Exit");
        var config = Config(entry, [entry, exit], []);

        Assert.Contains(config["outbounds"]!.AsArray(), item => (string?)item!["tag"] == "node-2");
        Assert.Equal("node-2", (string?)RoutingRules(config).Last()!["outboundTag"]);
    }

    [Fact]
    public void Exit_accepts_whitelist_entry_cascade_uuid()
    {
        var entry = Node(1, "Entry, WhitelistEntry") with { WhitelistExitNodeId = 2 };
        var exit = Node(2, "Exit");
        var inbounds = JsonNode.Parse(XrayConfigBuilder.Build(exit, [entry, exit], [], []))!["inbounds"]!.AsArray();

        Assert.Contains(Emails(inbounds, "vless-reality"), email => email == "node1");
    }

    private static string?[] Emails(JsonArray inbounds, string tag)
    {
        var inbound = inbounds.Single(item => (string?)item!["tag"] == tag)!;
        return inbound["settings"]!["clients"]!.AsArray().Select(item => (string?)item!["email"]).ToArray();
    }
}
