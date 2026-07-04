using FrogPlug.Core;
using Xunit;

namespace FrogPlug.Tests;

public sealed class XrayClientDiffTests
{
    [Fact]
    public void Finds_added_and_removed_clients_per_inbound()
    {
        var current = Config(["u1", "u2"], ["node1"]);
        var next = Config(["u2", "u3"], ["node1", "node2"]);

        var result = XrayClientDiff.Compare(current, next);

        Assert.True(result.IsClientOnlyChange);
        Assert.Equal(["u1"], result.Changes.Single(item => item.Tag == "vless-reality").RemovedEmails);
        Assert.Equal(["u3"], result.Changes.Single(item => item.Tag == "vless-reality").AddedClients
            .Select(item => item["email"]!.GetValue<string>()));
        Assert.Equal(["node2"], result.Changes.Single(item => item.Tag == "cascade").AddedClients
            .Select(item => item["email"]!.GetValue<string>()));
    }

    [Fact]
    public void Non_client_change_requires_full_restart()
    {
        var current = Config(["u1"], []);
        var next = Config(["u1"], []).Replace(
            "\"port\": 443",
            "\"port\": 444",
            StringComparison.Ordinal);

        var result = XrayClientDiff.Compare(current, next);

        Assert.False(result.IsClientOnlyChange);
    }

    private static string Config(string[] reality, string[] cascade)
    {
        return $$"""
        {
          "inbounds": [
            {
              "tag": "vless-reality",
              "port": 443,
              "settings": {
                "clients": [
        {{string.Join(",\n", reality.Select(email => $$"""{"email":"{{email}}","id":"{{email}}"}"""))}}
                ]
              }
            },
            {
              "tag": "cascade",
              "settings": {
                "clients": [
        {{string.Join(",\n", cascade.Select(email => $$"""{"email":"{{email}}","id":"{{email}}"}"""))}}
                ]
              }
            }
          ]
        }
        """;
    }
}
