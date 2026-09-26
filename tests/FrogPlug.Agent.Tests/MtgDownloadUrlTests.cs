using FrogPlug.Agent.Services;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class MtgDownloadUrlTests
{
    [Theory]
    [InlineData("v2.3.0", "amd64", "https://github.com/9seconds/mtg/releases/download/v2.3.0/mtg-2.3.0-linux-amd64.tar.gz")]
    [InlineData("v2.3.0", "arm64", "https://github.com/9seconds/mtg/releases/download/v2.3.0/mtg-2.3.0-linux-arm64.tar.gz")]
    public void Asset_name_has_version_without_v_prefix(string version, string arch, string expected)
    {
        Assert.Equal(expected, MtgManager.BuildDownloadUrl(version, arch));
    }
}
