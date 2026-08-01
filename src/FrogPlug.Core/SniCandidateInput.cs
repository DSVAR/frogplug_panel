namespace FrogPlug.Core;

public sealed record SniCandidateInput(
    string Host,
    bool ChainValid,
    bool IsExpired,
    bool IsSelfSigned,
    System.Security.Authentication.SslProtocols Protocol,
    string Alpn,
    string Issuer,
    IReadOnlyCollection<string> SubjectAlternativeNames,
    string? RedirectHost,
    long HandshakeMilliseconds,
    bool AllowPopularDomains = false);
