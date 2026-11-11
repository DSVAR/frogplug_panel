using Microsoft.AspNetCore.Http;

namespace FrogPlug.Server.Services;

public static class OpenFluxSubscriptionRequest
{
    public const string TransportsHeader = "X-OpenFlux-Transports";

    private static readonly HashSet<string> KnownTransports =
    [
        "vyandex",
        "yandex",
        "mailru",
        "telemost",
        "boards",
        "avito",
        "cupsonline",
        "oneme",
        "max"
    ];

    public static IReadOnlySet<string> Transports(HttpRequest request)
    {
        if (!request.Headers.TryGetValue(TransportsHeader, out var values))
        {
            return KnownTransports;
        }

        return values
            .SelectMany(value => value?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [])
            .Select(value => value.ToLowerInvariant())
            .Select(value => value == "max" ? "oneme" : value)
            .Where(KnownTransports.Contains)
            .ToHashSet(StringComparer.Ordinal);
    }
}
