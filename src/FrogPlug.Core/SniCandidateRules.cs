using System.Net;
using System.Security.Authentication;

namespace FrogPlug.Core;

public sealed record SniCandidateDecision(bool Accepted, string? Reason);

public static class SniCandidateRules
{
    private static readonly string[] CdnMarkers =
    [
        "cloudflare", "akamai", "fastly", "cloudfront", "edgecast", "incapsula",
        "sucuri", "ddos-guard", "stormwall", "qrator", "gcore", "cdn77",
        "bunny", "azureedge", "googleusercontent"
    ];

    public static SniCandidateDecision Evaluate(SniCandidateInput input)
    {
        if (!IsRealDomain(input.Host))
        {
            return Reject("hostname");
        }

        if (!input.AllowPopularDomains && PopularDomains.IsBlocked(input.Host))
        {
            return Reject("popular-domain");
        }

        if (ContainsCdnMarker(input))
        {
            return Reject("cdn");
        }

        if (input.Protocol != SslProtocols.Tls13)
        {
            return Reject("tls13");
        }

        if (!string.Equals(input.Alpn, "h2", StringComparison.Ordinal))
        {
            return Reject("h2");
        }

        if (!input.ChainValid || input.IsExpired || input.IsSelfSigned)
        {
            return Reject("certificate");
        }

        if (!string.IsNullOrWhiteSpace(input.RedirectHost) &&
            !string.Equals(input.Host, input.RedirectHost, StringComparison.OrdinalIgnoreCase))
        {
            return Reject("redirect");
        }

        return new SniCandidateDecision(true, null);
    }

    public static bool IsRealDomain(string? host)
    {
        if (string.IsNullOrWhiteSpace(host) || host.StartsWith("*.", StringComparison.Ordinal))
        {
            return false;
        }

        return IPAddress.TryParse(host, out _) == false &&
            Uri.CheckHostName(host) == UriHostNameType.Dns && host.Contains('.');
    }

    private static bool ContainsCdnMarker(SniCandidateInput input)
    {
        var values = input.SubjectAlternativeNames.Append(input.Host).Append(input.Issuer);
        return values.Any(value => CdnMarkers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase)));
    }

    private static SniCandidateDecision Reject(string reason)
    {
        return new SniCandidateDecision(false, reason);
    }
}
