namespace FrogPlug.Core;

public static class ChannelLinkParser
{
    public static ChannelLinkParseResult Parse(string? value)
    {
        var links = new ChannelLinkParseResult();
        foreach (var link in Split(value))
        {
            if (!links.TryAdd(link))
            {
                links.Errors.Add($"Повторяется ссылка: {link}");
            }
        }

        return links;
    }

    private static IEnumerable<string> Split(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([' ', '\t', '\r', '\n', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

public sealed class ChannelLinkParseResult
{
    private string? vyandexUrl;
    private string? yandexUrl;
    private string? mailruUrl;
    private string? telemostUrl;
    private string? boardsUrl;
    private string? avitoUrl;
    private string? cupsUrl;
    private string? maxToken;
    private long? maxUid;

    public string? VyandexUrl => vyandexUrl;
    public string? YandexUrl => yandexUrl;
    public string? MailruUrl => mailruUrl;
    public string? TelemostUrl => telemostUrl;
    public string? BoardsUrl => boardsUrl;
    public string? AvitoUrl => avitoUrl;
    public string? CupsUrl => cupsUrl;
    public string? MaxToken => maxToken;
    public long? MaxUid => maxUid;
    public List<string> Errors { get; } = [];

    public bool TryAdd(string link)
    {
        // Ссылка вида /edit/d/ подходит и для Volga, и для Docs: первая идёт в Volga, вторая — в Docs.
        if (OpenFluxChannelValidator.IsVyandexUrl(link) && (vyandexUrl is null || yandexUrl is not null))
        {
            return Set(ref vyandexUrl, link);
        }

        if (OpenFluxChannelValidator.IsYandexUrl(link))
        {
            return Set(ref yandexUrl, link);
        }

        if (OpenFluxChannelValidator.IsMailruUrl(link))
        {
            return Set(ref mailruUrl, link);
        }

        if (OpenFluxChannelValidator.IsTelemostUrl(link))
        {
            return Set(ref telemostUrl, link);
        }

        if (OpenFluxChannelValidator.IsBoardsUrl(link))
        {
            return Set(ref boardsUrl, link);
        }

        if (link.StartsWith("avito:", StringComparison.OrdinalIgnoreCase))
        {
            return SetRoom(ref avitoUrl, link, "avito:");
        }

        if (link.StartsWith("cupsonline:", StringComparison.OrdinalIgnoreCase))
        {
            return SetRoom(ref cupsUrl, link, "cupsonline:");
        }

        if (OpenFluxChannelValidator.IsMaxLink(link))
        {
            if (maxToken is not null)
            {
                return false;
            }

            OpenFluxChannelValidator.TryParseMaxLink(link, out var uid, out var token);
            maxUid = long.Parse(uid);
            maxToken = token;
            return true;
        }

        Errors.Add($"Неподдерживаемая ссылка: {link}");
        return true;
    }

    private bool SetRoom(ref string? destination, string link, string prefix)
    {
        var rooms = link[prefix.Length..];
        if (!OpenFluxChannelValidator.IsRooms(rooms))
        {
            Errors.Add($"Неподдерживаемая ссылка: {link}");
            return true;
        }

        return Set(ref destination, rooms);
    }

    private static bool Set(ref string? destination, string value)
    {
        if (destination is not null)
        {
            return false;
        }

        destination = value;
        return true;
    }
}
