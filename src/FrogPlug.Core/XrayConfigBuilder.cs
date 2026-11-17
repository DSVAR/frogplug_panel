using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrogPlug.Core;

public static class XrayConfigBuilder
{
    public static string Build(
        NodeSpec self,
        IReadOnlyList<NodeSpec> allNodes,
        IReadOnlyList<UserSpec> activeUsers,
        IReadOnlyList<RuleSpec> rules)
    {
        var enabledNodes = allNodes.Where(node => node.IsEnabled).ToArray();
        var cascadeNodes = enabledNodes
            .Where(node => node.Id != self.Id)
            .Where(node => CascadesTo(node, self, rules, enabledNodes))
            .ToArray();
        var inbounds = new List<object>
        {
            new { tag = "api", listen = "127.0.0.1", port = NodePorts.Api, protocol = "dokodemo-door", settings = new { address = "127.0.0.1" } }
        };
        // Настройка входа Reality.
        var hasUserRealityInbound = self.RealityEnabled && self.HasRole("Entry");
        var realityClients = new List<object>();
        if (hasUserRealityInbound)
        {
            realityClients.AddRange(activeUsers
                .Where(user => user.IsEnabled && (user.AllowReality || (self.HasRole("WhitelistEntry") && user.AllowWhitelist)))
                .Select(user => new { id = user.VlessUuid, email = $"u{user.Id}", flow = "xtls-rprx-vision" }));
        }

        // Каскад на другие ноды.
        realityClients.AddRange(cascadeNodes
            .Select(node => new { id = node.CascadeUuid, email = $"node{node.Id}", flow = "xtls-rprx-vision" }));

        if (hasUserRealityInbound || cascadeNodes.Length > 0)
        {
            var xhttpInbound = self.XhttpEnabled && self.HasRole("Entry") &&
                (self.MaskMode == MaskMode.ForeignSite || !string.IsNullOrWhiteSpace(self.Domain));
            // XHTTP внутри Reality через fallback.
            object realitySettingsBlock = xhttpInbound
                ? new { clients = realityClients, decryption = "none", fallbacks = new[] { new { dest = NodePorts.Xhttp } } }
                : new { clients = realityClients, decryption = "none" };
            inbounds.Add(new
            {
                tag = "vless-reality",
                listen = "127.0.0.1",
                port = NodePorts.Reality,
                protocol = "vless",
                settings = realitySettingsBlock,
                streamSettings = new
                {
                    network = "tcp",
                    security = "reality",
                    sockopt = new { acceptProxyProtocol = true },
                    realitySettings = new
                    {
                        dest = RealityDestination(self),
                        xver = self.MaskMode == MaskMode.OwnDomain ? 1 : 0,
                        serverNames = new[] { RealityServerName(self) },
                        privateKey = self.RealityPrivateKey,
                        shortIds = new[] { self.RealityShortId }
                    }
                },
                sniffing = new { enabled = true, destOverride = new[] { "http", "tls", "quic" }, routeOnly = true }
            });
            if (xhttpInbound)
            {
                inbounds.Add(new
                {
                    tag = "vless-xhttp",
                    listen = "127.0.0.1",
                    port = NodePorts.Xhttp,
                    protocol = "vless",
                    settings = new
                    {
                        clients = activeUsers
                            .Where(user => user.IsEnabled && user.AllowXhttp)
                            .Select(user => new { id = user.VlessUuid, email = $"u{user.Id}" }),
                        decryption = "none"
                    },
                    streamSettings = new
                    {
                        network = "xhttp",
                        security = "none",
                        xhttpSettings = new { path = self.XhttpPath, mode = self.XhttpMode }
                    },
                    sniffing = new { enabled = true, destOverride = new[] { "http", "tls", "quic" }, routeOnly = true }
                });
            }
        }

        inbounds.Add(new
        {
            tag = "socks-local",
            listen = "127.0.0.1",
            port = NodePorts.Socks,
            protocol = "socks",
            settings = new { auth = "noauth", udp = true },
            sniffing = new { enabled = true, destOverride = new[] { "http", "tls", "quic" } }
        });

        // Xray-balancer pools.
        var useExitPoolByDefault = self.HasRole("Entry") && self.HasRole("RuExit") && !self.HasRole("WhitelistEntry");
        var rolePools = BuildRolePools(self, enabledNodes, rules, useExitPoolByDefault);
        var targets = rules.Where(rule => rule.IsEnabled)
            .SelectMany(rule => NodesForTarget(rule.Target, self, enabledNodes))
            .Append(WhitelistExitTarget(self, enabledNodes))
            .Where(target => target.StartsWith("node-", StringComparison.Ordinal))
            .Concat(rolePools.Values.SelectMany(pool => pool.Select(node => $"node-{node.Id}")))
            .Distinct()
            .ToArray();
        var outbounds = new List<object>
        {
            new { tag = "direct", protocol = "freedom", settings = new { domainStrategy = "UseIPv4" } },
            new { tag = "block", protocol = "blackhole" }
        };
        foreach (var target in targets)
        {
            var id = int.Parse(target[5..]);
            var node = enabledNodes.First(item => item.Id == id);
            outbounds.Add(new
            {
                tag = target,
                protocol = "vless",
                settings = new
                {
                    vnext = new[]
                    {
                        new
                        {
                            address = node.Address,
                port = NodePorts.Public,
                            users = new[] { new { id = node.CascadeUuid, flow = "xtls-rprx-vision", encryption = "none" } }
                        }
                    }
                },
                streamSettings = new
                {
                    network = "tcp",
                    security = "reality",
                    realitySettings = new
                    {
                    serverName = RealityServerName(node),
                        publicKey = node.RealityPublicKey,
                        shortId = node.RealityShortId,
                        fingerprint = node.RealityFingerprint
                    }
                }
            });
        }

        var balancers = rolePools
            .Where(pair => pair.Value.Count > 1)
            .Select(pair => new
            {
                tag = BalancerTag(pair.Key),
                selector = pair.Value.Select(node => $"node-{node.Id}").ToArray(),
                fallbackTag = "block",
                strategy = new { type = "roundRobin" }
            })
            .ToArray();

        // Routing rules.
        var routingRules = new List<object>
        {
            new { type = "field", inboundTag = new[] { "api" }, outboundTag = "api" },
            new { type = "field", ip = new[] { "geoip:private" }, outboundTag = "block" }
        };
        var blockRules = rules.Where(rule => rule.IsEnabled && string.Equals(rule.Target, "block", StringComparison.OrdinalIgnoreCase))
            .OrderBy(rule => rule.Order);
        routingRules.AddRange(blockRules.Select(rule => ToRoutingRule(rule, RouteDestination.Outbound("block"))));
        var cascadeEmails = cascadeNodes.Select(node => $"node{node.Id}").ToArray();
        if (cascadeEmails.Length > 0)
        {
            routingRules.Add(new { type = "field", user = cascadeEmails, outboundTag = "direct" });
        }
        var nonBlockRules = rules.Where(rule => rule.IsEnabled &&
            !string.Equals(rule.Target, "block", StringComparison.OrdinalIgnoreCase));
        if (self.HasRole("WhitelistEntry"))
        {
            nonBlockRules = nonBlockRules.Where(rule => string.Equals(rule.Target, "direct", StringComparison.OrdinalIgnoreCase));
        }

        routingRules.AddRange(nonBlockRules
            .OrderBy(rule => rule.Order)
            .Select(rule => ToRoutingRule(rule, ResolveRouteTarget(rule.Target, self, enabledNodes, rolePools))));
        var whitelistExit = WhitelistExitTarget(self, enabledNodes);
        if (whitelistExit.StartsWith("node-", StringComparison.Ordinal))
        {
            routingRules.Add(new { type = "field", inboundTag = new[] { "vless-reality", "vless-xhttp" }, outboundTag = whitelistExit });
        }
        else if (useExitPoolByDefault)
        {
            routingRules.Add(ToDefaultExitRule(ResolveRouteTarget("role:Exit", self, enabledNodes, rolePools)));
        }
        var routing = new Dictionary<string, object>
        {
            ["domainStrategy"] = "IPIfNonMatch",
            ["rules"] = routingRules
        };
        if (balancers.Length > 0)
        {
            routing["balancers"] = balancers;
        }
        var config = new Dictionary<string, object>
        {
            ["log"] = new { loglevel = "warning", access = "none" },
            // Статистика трафика и API ядра.
            ["api"] = new { tag = "api", services = new[] { "StatsService", "HandlerService" } },
            ["stats"] = new { },
            ["policy"] = new
            {
                levels = new Dictionary<string, object>
                {
                    ["0"] = new { statsUserUplink = true, statsUserDownlink = true }
                },
                system = new
                {
                    statsInboundUplink = true,
                    statsInboundDownlink = true,
                    statsOutboundUplink = true,
                    statsOutboundDownlink = true
                }
            },
            ["inbounds"] = inbounds,
            ["outbounds"] = outbounds,
            ["routing"] = routing
        };
        if (balancers.Length > 0)
        {
            config["observatory"] = new
            {
                subjectSelector = balancers.SelectMany(item => item.selector).ToArray(),
                probeUrl = "https://www.gstatic.com/generate_204",
                probeInterval = "10s",
                enableConcurrency = true
            };
        }
        return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    }

    private static Dictionary<string, List<NodeSpec>> BuildRolePools(
        NodeSpec self,
        IReadOnlyList<NodeSpec> nodes,
        IReadOnlyList<RuleSpec> rules,
        bool useExitPoolByDefault)
    {
        var roles = rules.Where(rule => rule.IsEnabled && rule.Target.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
            .Select(rule => rule.Target[5..])
            .Append(useExitPoolByDefault ? "Exit" : string.Empty)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return roles.ToDictionary(
            role => role,
            role => NodesForRole(role, self, nodes).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static RouteDestination ResolveRouteTarget(
        string target,
        NodeSpec self,
        IReadOnlyList<NodeSpec> nodes,
        IReadOnlyDictionary<string, List<NodeSpec>> rolePools)
    {
        if (string.Equals(target, "direct", StringComparison.OrdinalIgnoreCase))
        {
            return RouteDestination.Outbound("direct");
        }
        if (target.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
        {
            var role = target[5..];
            var pool = rolePools.GetValueOrDefault(role) ?? [];
            if (pool.Count > 1)
            {
                return RouteDestination.Balancer(BalancerTag(role));
            }
            if (pool.Count == 1)
            {
                return RouteDestination.Outbound($"node-{pool[0].Id}");
            }
            return self.HasRole(role) ? RouteDestination.Outbound("direct") : RouteDestination.Outbound("block");
        }
        if (target.StartsWith("node:", StringComparison.OrdinalIgnoreCase) && int.TryParse(target[5..], out var id))
        {
            if (id == self.Id)
            {
                return RouteDestination.Outbound("direct");
            }

            if (nodes.Any(item => item.Id == id))
            {
                return RouteDestination.Outbound($"node-{id}");
            }

            return RouteDestination.Outbound("block");
        }
        if (string.Equals(target, "block", StringComparison.OrdinalIgnoreCase))
        {
            return RouteDestination.Outbound("block");
        }

        return RouteDestination.Outbound("block");
    }

    private static string RealityServerName(NodeSpec node)
    {
        return node.MaskMode == MaskMode.OwnDomain
            ? node.Domain ?? string.Empty
            : node.RealitySni;
    }

    private static string RealityDestination(NodeSpec node)
    {
        return node.MaskMode == MaskMode.OwnDomain
            ? $"127.0.0.1:{NodePorts.CaddyTls}"
            : $"{node.RealitySni}:443";
    }

    private static bool CascadesTo(
        NodeSpec source,
        NodeSpec destination,
        IReadOnlyList<RuleSpec> rules,
        IReadOnlyList<NodeSpec> nodes)
    {
        if (source.HasRole("WhitelistEntry") && source.WhitelistExitNodeId == destination.Id)
        {
            return true;
        }

        return rules.Where(rule => rule.IsEnabled)
            .SelectMany(rule => NodesForTarget(rule.Target, source, nodes))
            .Any(target => target == $"node-{destination.Id}");
    }

    private static string WhitelistExitTarget(NodeSpec self, IReadOnlyList<NodeSpec> nodes)
    {
        return self.HasRole("WhitelistEntry") && self.WhitelistExitNodeId is { } id && nodes.Any(node => node.Id == id)
            ? $"node-{id}"
            : "direct";
    }

    private static IEnumerable<string> NodesForTarget(string target, NodeSpec self, IReadOnlyList<NodeSpec> nodes)
    {
        if (target.StartsWith("role:", StringComparison.OrdinalIgnoreCase))
        {
            return NodesForRole(target[5..], self, nodes).Select(node => $"node-{node.Id}");
        }
        if (target.StartsWith("node:", StringComparison.OrdinalIgnoreCase) && int.TryParse(target[5..], out var id) && id != self.Id && nodes.Any(node => node.Id == id))
        {
            return [$"node-{id}"];
        }
        return [];
    }

    private static IEnumerable<NodeSpec> NodesForRole(string role, NodeSpec self, IReadOnlyList<NodeSpec> nodes)
    {
        var pool = nodes.Where(node => node.Id != self.Id && node.HasRole(role));
        if (role.Equals("GoogleExit", StringComparison.OrdinalIgnoreCase) && !pool.Any())
        {
            pool = nodes.Where(node => node.Id != self.Id && node.HasRole("Exit"));
        }
        return pool;
    }

    private static string BalancerTag(string role) => $"balancer-{role.ToLowerInvariant()}";

    private static RoutingObject ToRoutingRule(RuleSpec rule, RouteDestination destination)
    {
        var field = new RoutingObject { type = "field", outboundTag = destination.OutboundTag, balancerTag = destination.BalancerTag };
        switch (rule.Kind.ToLowerInvariant())
        {
            case "geosite":
                field.domain = new[] { $"geosite:{rule.Value}" };
                break;
            case "geoip":
                field.ip = new[] { $"geoip:{rule.Value}" };
                break;
            case "domain":
                field.domain = new[] { $"domain:{rule.Value}" };
                break;
            case "full":
                field.domain = new[] { $"full:{rule.Value}" };
                break;
            case "ip":
                field.ip = new[] { rule.Value };
                break;
            case "port":
                field.port = rule.Value;
                break;
        }
        return field;
    }

    private static RoutingObject ToDefaultExitRule(RouteDestination destination) => new()
    {
        type = "field",
        inboundTag = new[] { "vless-reality", "vless-xhttp" },
        outboundTag = destination.OutboundTag,
        balancerTag = destination.BalancerTag
    };

    private sealed record RoutingObject
    {
        public string type { get; init; } = "field";
        public string? outboundTag { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? balancerTag { get; init; }
        public string[]? domain { get; set; }
        public string[]? ip { get; set; }
        public string? port { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string[]? inboundTag { get; init; }
    }

    private sealed record RouteDestination(string? OutboundTag, string? BalancerTag)
    {
        public static RouteDestination Outbound(string tag) => new(tag, null);
        public static RouteDestination Balancer(string tag) => new(null, tag);
    }
}
