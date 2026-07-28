using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class HaproxyConfigBuilderTests
{
    [Fact]
    public void Without_mtproto_everything_goes_to_reality_with_proxy_protocol()
    {
        var config = HaproxyConfigBuilder.Build(false, "tg.example.com");

        Assert.Contains("bind :443", config);
        Assert.Contains("default_backend frogplug-reality", config);
        Assert.Contains("server reality 127.0.0.1:4431 send-proxy-v2", config);
        Assert.DoesNotContain("frogplug-mtproto", config);
    }

    [Fact]
    public void Mtproto_host_is_routed_by_sni()
    {
        var config = HaproxyConfigBuilder.Build(true, " tg.example.com ");

        Assert.Contains("use_backend frogplug-mtproto if { req.ssl_sni -i tg.example.com }", config);
        Assert.Contains("server mtproto 127.0.0.1:2443 send-proxy-v2", config);
    }

    [Fact]
    public void Mtproto_without_host_is_not_routed()
    {
        var config = HaproxyConfigBuilder.Build(true, null);

        Assert.DoesNotContain("frogplug-mtproto", config);
    }

    [Fact]
    public void Idle_vpn_connections_are_not_cut_after_seconds()
    {
        var config = HaproxyConfigBuilder.Build(false, null);

        Assert.Contains("timeout client 1h", config);
        Assert.Contains("timeout server 1h", config);
        Assert.Contains("timeout tunnel 1h", config);
        Assert.DoesNotContain("\r\n", config);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Every_directive_is_on_its_own_line_and_file_ends_with_newline(bool mtproto)
    {
        var config = HaproxyConfigBuilder.Build(mtproto, "tg.example.com");
        var lines = config.Split('\n');

        Assert.EndsWith("\n", config);
        Assert.Contains("    tcp-request content accept if { req_ssl_hello_type 1 }", lines);
        Assert.Contains("    default_backend frogplug-reality", lines);
        Assert.All(lines, line => Assert.True(
            line.Split('}').Length <= 2 || line.Contains("req.ssl_sni"),
            $"Склеенные директивы: {line}"));
    }
}
