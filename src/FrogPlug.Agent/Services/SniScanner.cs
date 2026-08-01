using System.Collections.Concurrent;
using System.Net;
using FrogPlug.Core;

namespace FrogPlug.Agent.Services;

public sealed class SniScanner
{
    private readonly ISniNetwork network;

    public SniScanner(ISniNetwork network)
    {
        this.network = network;
    }

    public async Task<SniScanResponse> ScanAsync(
        IPAddress baseAddress,
        string? range,
        int? limit,
        CancellationToken token)
    {
        var addresses = BuildAddresses(baseAddress, range);
        var maximum = Math.Clamp(limit ?? 10, 1, 100);
        var candidates = new ConcurrentBag<SniScanCandidate>();
        var rejected = new ConcurrentDictionary<string, int>(StringComparer.Ordinal);
        using var cap = CancellationTokenSource.CreateLinkedTokenSource(token);
        cap.CancelAfter(TimeSpan.FromMinutes(2));
        await Parallel.ForEachAsync(addresses, new ParallelOptions
        {
            MaxDegreeOfParallelism = 16,
            CancellationToken = cap.Token
        }, async (address, cancellationToken) =>
        {
            if (candidates.Count >= maximum)
            {
                return;
            }

            await ScanAddressAsync(address, candidates, rejected, cancellationToken);
        });
        return new SniScanResponse(addresses.Count, candidates.OrderBy(x => x.HandshakeMs).ToArray(), rejected);
    }

    public static IReadOnlyList<IPAddress> BuildAddresses(IPAddress baseAddress, string? range)
    {
        if (baseAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            throw new ArgumentException("Требуется IPv4-адрес.", nameof(baseAddress));
        }

        var (network, prefix) = ParseRange(baseAddress, range);
        var count = 1 << (32 - prefix);
        if (count > 1024)
        {
            throw new ArgumentException("Диапазон не должен превышать 1024 адреса.", nameof(range));
        }
        var addresses = new List<IPAddress>(Math.Min(count, 1024));
        for (var offset = 0; offset < count && addresses.Count < 1024; offset++)
        {
            addresses.Add(FromUInt32(network + (uint)offset));
        }

        return addresses;
    }

    private async Task ScanAddressAsync(
        IPAddress address,
        ConcurrentBag<SniScanCandidate> candidates,
        ConcurrentDictionary<string, int> rejected,
        CancellationToken token)
    {
        var result = await network.ProbeAsync(address, TimeSpan.FromSeconds(3), token);
        if (result.Candidate is null)
        {
            AddRejected(rejected, result.RejectionReason ?? "connection");
            return;
        }

        var decision = SniCandidateRules.Evaluate(result.Candidate);
        if (!decision.Accepted)
        {
            AddRejected(rejected, decision.Reason!);
            return;
        }

        candidates.Add(new SniScanCandidate(result.Candidate.Host, address.ToString(),
            result.Candidate.HandshakeMilliseconds, true, true, result.Candidate.Issuer));
    }

    private static (uint Network, int Prefix) ParseRange(IPAddress baseAddress, string? range)
    {
        if (string.IsNullOrWhiteSpace(range))
        {
            return (ToUInt32(baseAddress) & 0xffffff00, 24);
        }

        var parts = range.Split('/', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
            !int.TryParse(parts[1], out var prefix) || prefix < 22 || prefix > 32)
        {
            throw new ArgumentException("Допустим только IPv4 CIDR от /22 до /32.", nameof(range));
        }

        var mask = prefix == 0 ? 0U : uint.MaxValue << (32 - prefix);
        return (ToUInt32(address) & mask, prefix);
    }

    private static void AddRejected(ConcurrentDictionary<string, int> rejected, string reason)
    {
        rejected.AddOrUpdate(reason, 1, (_, count) => count + 1);
    }

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    private static IPAddress FromUInt32(uint value)
    {
        return new IPAddress([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
    }
}
