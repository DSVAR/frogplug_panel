using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class AgentClientTimeoutTests
{
    [Theory]
    [InlineData("/status", 30)]
    [InlineData("/tgweb/apply", 180)]
    [InlineData("/openflux/apply", 180)]
    [InlineData("/xray/config", 180)]
    [InlineData("/mask/scan", 180)]
    [InlineData("/xray/update", 300)]
    public void Timeout_depends_on_request(string path, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), AgentClient.TimeoutFor(path));
    }
}
