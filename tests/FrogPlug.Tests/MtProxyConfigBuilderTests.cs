using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class MtProxyConfigBuilderTests
{
    [Fact]
    public void Build_start_script_uses_normalized_secret_and_loopback_stats_port()
    {
        var secret = "ee000000000000000000000000000000006e6f64652e6578616d706c652e636f6d";

        var script = MtProxyConfigBuilder.BuildStartScript(secret);

        Assert.Contains("-p 2399", script);
        Assert.Contains("-H 2398", script);
        Assert.DoesNotContain("--address", script);
        Assert.Contains("-S 00000000000000000000000000000000", script);
        Assert.Contains("--aes-pwd /etc/mtproxy/proxy-secret /etc/mtproxy/proxy-multi.conf", script);
    }
}
