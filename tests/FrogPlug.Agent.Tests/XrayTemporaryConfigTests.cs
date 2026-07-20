using FrogPlug.Agent.Services;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class XrayTemporaryConfigTests
{
    [Fact]
    public void Temporary_config_keeps_json_extension_for_xray_format_detection()
    {
        var path = XrayManager.TemporaryConfigPath(Path.Combine("etc", "xray", "config.json"));

        Assert.Equal(Path.Combine("etc", "xray", "config.new.json"), path);
    }
}
