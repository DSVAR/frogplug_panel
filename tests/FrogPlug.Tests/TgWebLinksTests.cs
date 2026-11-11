using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class TgWebLinksTests
{
    [Fact]
    public void Builds_official_web_proxy_link_with_the_transform_prefix_removed()
    {
        var secret = "ee00112233445566778899aabbccddeeff766e2e6578616d706c65";

        var link = TgWebLinks.Https("vpn.example", secret);

        Assert.Equal(
            "https://t.me/webproxy?server=vpn.example&secret=00112233445566778899aabbccddeeff",
            link);
    }
}
