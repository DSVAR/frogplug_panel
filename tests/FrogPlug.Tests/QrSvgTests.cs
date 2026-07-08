using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class QrSvgTests
{
    [Fact]
    public void Generates_real_qr_svg_for_subscription_url()
    {
        var url = "https://panel.example.com/sub/qGMTWc1gpfZODHovygjOeZsHsfSTv6_KqTfOigIGduw";

        var svg = QrSvg.Generate(url);

        Assert.StartsWith("<svg", svg);
        Assert.Contains("<path", svg);
    }

    [Fact]
    public void Qr_size_matches_standard_version_grid()
    {
        var size = QrSvg.Size("https://panel.example.com/sub/qGMTWc1gpfZODHovygjOeZsHsfSTv6_KqTfOigIGduw");

        Assert.True(size >= 21);
        Assert.Equal(0, (size - 17) % 4);
    }
}
