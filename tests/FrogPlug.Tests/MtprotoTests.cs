using FrogPlug.Core;
using Xunit;

namespace FrogPlug.Tests;

public sealed class MtprotoTests
{
    [Fact]
    public void Secret_round_trip_returns_domain()
    {
        var secret = MtprotoSecret.Generate("node.example.com");

        Assert.Equal("node.example.com", MtprotoSecret.Parse(secret));
        Assert.StartsWith("ee", secret);
        Assert.Equal(2 + 32 + "node.example.com".Length * 2, secret.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dd0011")]
    [InlineData("ee0011")]
    [InlineData("ee00000000000000000000000000000000zz")]
    public void Invalid_secret_is_rejected(string secret)
    {
        Assert.Throws<ArgumentException>(() => MtprotoSecret.Parse(secret));
    }

    [Fact]
    public void Config_matches_golden_toml()
    {
        const string secret = "ee000000000000000000000000000000006e6f64652e6578616d706c652e636f6d";
        var config = MtgConfigBuilder.Build(secret, 2443, 443);

        Assert.Equal(
            "secret = \"ee000000000000000000000000000000006e6f64652e6578616d706c652e636f6d\"\n" +
            "bind-to = \"127.0.0.1:2443\"\nproxy-protocol-listener = true\n\n[domain-fronting]\n" +
            "host = \"node.example.com\"\nport = 443\n",
            config);
    }

    [Fact]
    public void Own_caddy_fronting_uses_loopback_with_proxy_protocol()
    {
        const string secret = "ee000000000000000000000000000000006e6f64652e6578616d706c652e636f6d";
        var config = MtgConfigBuilder.Build(secret, 2443, 8443, "127.0.0.1");

        Assert.EndsWith("[domain-fronting]\nhost = \"127.0.0.1\"\nport = 8443\nproxy-protocol = true\n", config);
    }

    [Fact]
    public void Links_use_mtproto_proxy_formats()
    {
        const string secret = "ee000000000000000000000000000000006e6f64652e6578616d706c652e636f6d";
        Assert.Equal(
            $"tg://proxy?server=node.example.com&port=2443&secret={secret}",
            MtprotoLinks.Tg("node.example.com", 2443, secret));
        Assert.Equal(
            $"https://t.me/proxy?server=node.example.com&port=2443&secret={secret}",
            MtprotoLinks.Https("node.example.com", 2443, secret));
    }
}
