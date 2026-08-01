namespace FrogPlug.Core;

public sealed record SniScanRequest(string? Range = null, int? Limit = null);

public sealed record SniScanResponse(
    int Scanned,
    SniScanCandidate[] Candidates,
    IReadOnlyDictionary<string, int> Rejected);

public sealed record SniScanCandidate(
    string Host,
    string Ip,
    long HandshakeMs,
    bool Tls13,
    bool H2,
    string Issuer);