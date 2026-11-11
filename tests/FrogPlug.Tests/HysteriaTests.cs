using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class HysteriaTests
{
    [Fact]
    public void ConfigMasqueradesAsStubSite()
    {
        var config = HysteriaConfigBuilder.Build("/c/a.crt", "/c/a.key", 443, [new HysteriaUser("2", "p\"w"), new HysteriaUser("1", "x")]);

        Assert.Contains("listen: :443", config);
        Assert.Contains("type: file", config);
        Assert.Contains("dir: /var/lib/frogplug/site", config);
        Assert.DoesNotContain("127.0.0.1:8080", config);
        Assert.Contains("\"2\": \"p\\\"w\"", config);
        Assert.True(config.IndexOf("\"1\":", StringComparison.Ordinal) < config.IndexOf("\"2\":", StringComparison.Ordinal));
    }

    [Fact]
    public void ObfsReplacesMasquerade()
    {
        var config = HysteriaConfigBuilder.Build("/c/a.crt", "/c/a.key", 443, [new HysteriaUser("1", "x")], "pw");

        Assert.Contains("type: salamander", config);
        Assert.Contains("password: \"pw\"", config);
        Assert.DoesNotContain("masquerade", config);
    }

    [Fact]
    public void LinkCarriesObfs()
    {
        var password = HysteriaLinks.ObfsPassword(Guid.Parse("11111111-2222-3333-4444-555555555555"));
        var link = HysteriaLinks.Build("7", "secret", "fl.example.com", 443, "N", password);

        Assert.Equal(32, password.Length);
        Assert.Equal(password, HysteriaLinks.ObfsPassword(Guid.Parse("11111111-2222-3333-4444-555555555555")));
        Assert.Equal($"hysteria2://7:secret@fl.example.com:443/?sni=fl.example.com&obfs=salamander&obfs-password={password}#N", link);
    }

    [Fact]
    public void LinkUsesDomainAsSni()
    {
        var link = HysteriaLinks.Build("7", "secret", "fl.example.com", 443, "Узел");

        Assert.Equal("hysteria2://7:secret@fl.example.com:443/?sni=fl.example.com#%D0%A3%D0%B7%D0%B5%D0%BB", link);
    }
}
