using System.Security.Authentication;
using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class SniCandidateRulesTests
{
    [Fact]
    public void Evaluate_AcceptsValidNonCdnCandidate()
    {
        var decision = SniCandidateRules.Evaluate(Candidate("origin.example", issuer: "CN=Origin"));

        Assert.True(decision.Accepted);
    }

    [Theory]
    [InlineData("google.com", "CN=Origin", "popular-domain")]
    [InlineData("origin.example", "CN=Cloudflare Inc", "cdn")]
    [InlineData("192.0.2.2", "CN=Origin", "hostname")]
    public void Evaluate_RejectsUnsafeCandidate(string host, string issuer, string reason)
    {
        var decision = SniCandidateRules.Evaluate(Candidate(host, issuer));

        Assert.False(decision.Accepted);
        Assert.Equal(reason, decision.Reason);
    }

    [Fact]
    public void Evaluate_RejectsRedirectToAnotherHost()
    {
        var decision = SniCandidateRules.Evaluate(Candidate("origin.example", redirectHost: "other.example"));

        Assert.Equal("redirect", decision.Reason);
    }

    [Fact]
    public void Evaluate_AllowsPopularDomainForWhitelistEntry()
    {
        var decision = SniCandidateRules.Evaluate(Candidate("gosuslugi.ru") with { AllowPopularDomains = true });

        Assert.True(decision.Accepted);
        Assert.Null(decision.Reason);
    }

    private static SniCandidateInput Candidate(string host, string issuer = "CN=Origin", string? redirectHost = null)
    {
        return new SniCandidateInput(host, true, false, false, SslProtocols.Tls13,
            "h2", issuer, [host], redirectHost, 10);
    }
}
