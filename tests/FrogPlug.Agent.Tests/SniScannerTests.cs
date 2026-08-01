using System.Net;
using System.Security.Authentication;
using FrogPlug.Agent.Services;
using FrogPlug.Core;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class SniScannerTests
{
    [Fact]
    public async Task ScanAsync_ReturnsAcceptedCandidatesSortedByHandshake()
    {
        var network = new FakeSniNetwork(address => address.ToString().EndsWith(".1", StringComparison.Ordinal)
            ? Candidate("slow.example", 40)
            : Candidate("fast.example", 10));
        var scanner = new SniScanner(network);

        var result = await scanner.ScanAsync(IPAddress.Parse("192.0.2.1"), "192.0.2.0/31", 10, default);

        Assert.Equal(2, result.Scanned);
        Assert.Equal(["fast.example", "slow.example"], result.Candidates.Select(item => item.Host));
    }

    [Fact]
    public async Task ScanAsync_CountsRuleRejections()
    {
        var network = new FakeSniNetwork(_ => Candidate("google.com", 10));
        var scanner = new SniScanner(network);

        var result = await scanner.ScanAsync(IPAddress.Parse("192.0.2.1"), "192.0.2.1/32", 10, default);

        Assert.Empty(result.Candidates);
        Assert.Equal(1, result.Rejected["popular-domain"]);
    }

    [Fact]
    public void BuildAddresses_RejectsRangeLargerThan1024Addresses()
    {
        Assert.Throws<ArgumentException>(() =>
            SniScanner.BuildAddresses(IPAddress.Parse("192.0.2.1"), "192.0.0.0/21"));
    }

    private static SniProbeResult Candidate(string host, long handshakeMilliseconds)
    {
        return new SniProbeResult(new SniCandidateInput(host, true, false, false, SslProtocols.Tls13,
            "h2", "CN=Example", [host], null, handshakeMilliseconds), null);
    }

    private sealed class FakeSniNetwork : ISniNetwork
    {
        private readonly Func<IPAddress, SniProbeResult> result;

        public FakeSniNetwork(Func<IPAddress, SniProbeResult> result)
        {
            this.result = result;
        }

        public Task<SniProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken token)
        {
            return Task.FromResult(result(address));
        }
    }
}
