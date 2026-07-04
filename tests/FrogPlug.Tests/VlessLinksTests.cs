using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class VlessLinksTests
{
    private static NodeSpec Node() => new()
    {
        Address = "example.com",
        Domain = "vpn.example",
        RealityPort = 443,
        RealityServerName = "sni.example",
        RealityPublicKey = "pub key",
        RealityShortId = "sid",
        XhttpPath = "/x y"
        ,MaskMode = MaskMode.ForeignSite
        ,RealitySni = "sni.example"
    };

    private static UserSpec User() => new()
    {
        VlessUuid = Guid.Parse("11111111-1111-1111-1111-111111111111")
    };

    [Fact]
    public void Reality_link_is_exact()
    {
        var expected = "vless://11111111-1111-1111-1111-111111111111@example.com:443?encryption=none&flow=xtls-rprx-vision&security=reality";
        expected += "&sni=sni.example&fp=chrome&pbk=pub%20key&sid=sid&type=tcp#Remark";
        var actual = VlessLinks.Reality(Node(), User(), "Remark");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Xhttp_link_is_exact()
    {
        var expected = "vless://11111111-1111-1111-1111-111111111111@example.com:443?encryption=none&security=reality";
        expected += "&sni=sni.example&fp=chrome&pbk=pub%20key&sid=sid&type=xhttp&path=%2Fx%20y&mode=auto#Remark";
        var actual = VlessLinks.Xhttp(Node(), User(), "Remark");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Remark_is_encoded_with_country_flag()
    {
        var node = Node() with { Name = "N", Country = "CZ" };
        var actual = VlessLinks.Reality(node, User(), NodeRemarks.Reality(node));
        Assert.Contains("#%F0%9F%87%A8%F0%9F%87%BF", actual);
    }

    [Fact]
    public void Czech_remark_has_flag()
    {
        var node = Node() with { Name = "N", Country = "CZ" };
        var actual = NodeRemarks.Reality(node);
        Assert.StartsWith("🇨🇿", actual);
    }

    [Fact]
    public void Xhttp_remark_has_protocol()
    {
        var node = Node() with { Name = "N", Country = "RU" };
        var actual = NodeRemarks.Xhttp(node);
        Assert.EndsWith("XHTTP", actual);
    }

    [Fact]
    public void Unknown_country_uses_globe()
    {
        var node = Node() with { Country = string.Empty };
        var actual = NodeRemarks.Reality(node);
        Assert.StartsWith("🌐", actual);
    }

    [Fact]
    public void Path_is_url_encoded()
    {
        var actual = VlessLinks.Xhttp(Node(), User(), "x");
        Assert.Contains("path=%2Fx%20y", actual);
    }
}
