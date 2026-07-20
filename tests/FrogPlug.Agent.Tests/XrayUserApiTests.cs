using System.Text.Json.Nodes;
using FrogPlug.Agent.Services;
using FrogPlug.Core;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class XrayUserApiTests
{
    private const string Config = """
        {"inbounds":[{"tag":"vless-reality","listen":"127.0.0.1","port":4431,"protocol":"vless",
        "settings":{"clients":[],"decryption":"none"}}]}
        """;

    [Fact]
    public void Additions_carry_listen_port_and_protocol_of_the_inbound()
    {
        var client = new JsonObject { ["id"] = "11111111-1111-1111-1111-111111111111", ["email"] = "u7" };
        var change = new XrayClientChange("vless-reality", [], [client]);

        var json = JsonNode.Parse(XrayManager.BuildAdditionsConfig(change, Config))!;
        var inbound = json["inbounds"]![0]!;

        Assert.Equal("vless-reality", (string?)inbound["tag"]);
        Assert.Equal("127.0.0.1", (string?)inbound["listen"]);
        Assert.Equal(4431, (int?)inbound["port"]);
        Assert.Equal("vless", (string?)inbound["protocol"]);
        Assert.Equal("u7", (string?)inbound["settings"]!["clients"]![0]!["email"]);
    }

    [Theory]
    [InlineData("processing inbound: vless-reality\nadd user: u1\nresult: ok\nAdded 1 user(s) in total.", "Added", 1)]
    [InlineData("failed to build config: ...\nAdded 0 user(s) in total.", "Added", 0)]
    [InlineData("remove user: u1\nRemoved 2 user(s) in total.", "Removed", 2)]
    [InlineData("rpc error", "Removed", -1)]
    public void Reported_count_is_read_from_xray_output(string output, string verb, int expected)
    {
        Assert.Equal(expected, XrayManager.ReportedCount(output, verb));
    }
}
