using System.Text.Json;
using System.Text.Json.Nodes;
using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class XrayConfigGoldenTests
{
    [Fact]
    public void Single_entry_matches_golden()
    {
        Verify("single-entry.json", BuildSingleEntry());
    }

    [Fact]
    public void Entry_with_domain_matches_golden()
    {
        Verify("entry-with-domain.json", BuildEntryWithDomain());
    }

    [Fact]
    public void Two_nodes_ru_cascade_matches_golden()
    {
        Verify("two-nodes-ru-cascade.json", BuildTwoNodesCascade());
    }

    [Fact]
    public void Role_resolves_to_self_matches_golden()
    {
        Verify("role-resolves-to-self.json", BuildRoleResolvesToSelf());
    }

    [Fact]
    public void Disabled_users_excluded_matches_golden()
    {
        Verify("disabled-users-excluded.json", BuildDisabledUsersExcluded());
    }

    private static string BuildSingleEntry()
    {
        var node = Node(1, "Entry", "198.51.100.1");
        var user = User(1, true);
        return XrayConfigBuilder.Build(node, [node], [user], []);
    }

    private static string BuildEntryWithDomain()
    {
        var node = Node(1, "Entry", "198.51.100.1") with
        {
            Domain = "vpn.example",
            XhttpPath = "/xhttp"
        };
        return XrayConfigBuilder.Build(node, [node], [User(1, true)], []);
    }

    private static string BuildTwoNodesCascade()
    {
        var self = Node(1, "Entry", "203.0.113.1") with { Country = "RU" };
        var target = Node(2, "Exit", "203.0.113.2") with
        {
            Country = "RU",
            RealityPublicKey = "node-2-public-key",
            RealityShortId = "node2sid"
        };
        var rules = new[] { new RuleSpec { Kind = "Domain", Value = "example.ru", Target = "node:2" } };
        return XrayConfigBuilder.Build(self, [self, target], [User(1, true)], rules);
    }

    private static string BuildRoleResolvesToSelf()
    {
        var node = Node(1, "Entry", "198.51.100.1");
        var rules = new[] { new RuleSpec { Kind = "Domain", Value = "self.example", Target = "role:Entry" } };
        return XrayConfigBuilder.Build(node, [node], [User(1, true)], rules);
    }

    private static string BuildDisabledUsersExcluded()
    {
        var node = Node(1, "Entry", "198.51.100.1");
        var users = new[] { User(1, true), User(2, false) };
        return XrayConfigBuilder.Build(node, [node], users, []);
    }

    private static NodeSpec Node(int id, string roles, string address)
    {
        return new NodeSpec
        {
            Id = id,
            Name = $"Node {id}",
            Address = address,
            Country = "CZ",
            Roles = roles,
            RealityPort = 443,
            RealityPrivateKey = $"private-{id}",
            RealityPublicKey = $"public-{id}",
            RealityShortId = $"short-{id}",
            RealityServerName = "example.com",
            RealityDest = "example.com:443",
            XhttpPath = "/xhttp",
            CascadeUuid = Guid.Parse($"00000000-0000-0000-0000-{id:000000000000}")
        };
    }

    private static UserSpec User(int id, bool enabled)
    {
        return new UserSpec
        {
            Id = id,
            VlessUuid = Guid.Parse($"11111111-1111-1111-1111-{id:000000000000}"),
            IsEnabled = enabled
        };
    }

    private static void Verify(string fileName, string actualJson)
    {
        var actual = JsonNode.Parse(actualJson)!;
        var path = FindGoldenPath(fileName);
        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(path, actual.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        var expected = JsonNode.Parse(File.ReadAllText(path));
        Assert.True(JsonNode.DeepEquals(expected, actual), $"Golden file differs: {fileName}");
    }

    private static string FindGoldenPath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "tests", "FrogPlug.Tests", "Golden", fileName);
            if (File.Exists(candidate) || Directory.Exists(Path.GetDirectoryName(candidate)))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Golden directory not found.");
    }
}
