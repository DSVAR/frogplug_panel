using FrogPlug.Agent;
using FrogPlug.Core;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class AgentLogicTests
{
    [Fact] public void MissingBearerIsRejected() => Assert.False(BearerAuthMiddleware.IsAuthorized("", "secret"));
    [Fact] public void WrongBearerIsRejected() => Assert.False(BearerAuthMiddleware.IsAuthorized("Bearer wrong", "secret"));
    [Fact] public void RightBearerIsAccepted() => Assert.True(BearerAuthMiddleware.IsAuthorized("Bearer secret", "secret"));
    [Fact] public void BearerComparisonIsCaseInsensitiveForScheme() => Assert.True(BearerAuthMiddleware.IsAuthorized("bearer secret", "secret"));
    [Fact] public void BearerComparisonRejectsPrefixOnly() => Assert.False(BearerAuthMiddleware.IsAuthorized("Bearer ", "secret"));
    [Fact] public void DigestParserReadsSha2() => Assert.Equal(new string('a', 64), AgentParsers.ParseDigest("SHA2-256=" + new string('a', 64)));
    [Fact] public void DigestParserAcceptsLowerCaseMarker() => Assert.Equal(new string('b', 64), AgentParsers.ParseDigest("sha2-256=" + new string('b', 64)));
    [Fact] public void DigestParserRejectsShortValue() => Assert.Null(AgentParsers.ParseDigest("SHA2-256=abc"));
    [Fact] public void DigestParserReadsReleaseFormat() => Assert.Equal(
        new string('b', 64),
        AgentParsers.ParseDigest("MD5= 91f9\nSHA2-256= " + new string('B', 64) + "\nSHA2-512= ff"));
    [Fact] public void ShaParserReadsChecksum() => Assert.Equal(new string('c', 64), AgentParsers.ParseSha256Sum(new string('c', 64) + "  geoip.dat"));
    [Fact] public void ShaParserRejectsInvalidChecksum() => Assert.Null(AgentParsers.ParseSha256Sum("not-a-checksum"));
    [Fact]
    public void StatsParserReadsUplink()
    {
        var json = "{\"stat\":[{\"name\":\"user>>>u1>>>traffic>>>uplink\",\"value\":12}]}";
        Assert.Equal(12, AgentParsers.ParseStats(json).Users["u1"].Up);
    }

    [Fact]
    public void StatsParserReadsDownlink()
    {
        var json = "{\"stat\":[{\"name\":\"user>>>u1>>>traffic>>>downlink\",\"value\":13}]}";
        Assert.Equal(13, AgentParsers.ParseStats(json).Users["u1"].Down);
    }


    [Fact] public void StatsParserIgnoresMissingName() => Assert.Empty(AgentParsers.ParseStats("{\"stat\":[{\"value\":3}]} ").Users);
    [Fact] public void StatsParserIgnoresEmptyName() => Assert.Empty(AgentParsers.ParseStats("{\"stat\":[{\"name\":\"user>>>\"}]} ").Users);
    [Fact]
    public void OptionsHaveSecureDefaults()
    {
        var options = new AgentOptions();
        Assert.Equal("127.0.0.1:9080", options.Listen);
    }
    [Fact] public void OptionsHaveXrayBinaryDefault() { var options = new AgentOptions(); Assert.EndsWith("xray", options.XrayBinary); }
    [Fact] public void OptionsHaveGeoDirectoryDefault() { var options = new AgentOptions(); Assert.Contains("xray", options.GeoDir); }
    [Fact]
    public void TrafficStatsIsCaseInsensitive()
    {
        var users = new Dictionary<string, TrafficStat>(StringComparer.OrdinalIgnoreCase)
        {
            ["u1"] = new(1, 2),
        };
        var stats = new TrafficStats(users);
        Assert.Equal(1, stats.Users["U1"].Up);
    }
}
