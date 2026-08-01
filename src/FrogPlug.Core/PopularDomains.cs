namespace FrogPlug.Core;

public static class PopularDomains
{
    private static readonly string[] Names =
    [
        "microsoft.com", "apple.com", "google.com", "cloudflare.com", "amazon.com",
        "yahoo.com", "bing.com", "github.com", "youtube.com", "facebook.com",
        "instagram.com", "whatsapp.com", "telegram.org", "yandex.ru", "vk.com",
        "mail.ru", "ozon.ru", "wildberries.ru", "gosuslugi.ru", "sberbank.ru",
        "tinkoff.ru"
    ];

    public static bool IsBlocked(string? hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
        {
            return false;
        }

        var value = hostname.Trim().TrimEnd('.').ToLowerInvariant();
        return Names.Any(name => value == name || value.EndsWith("." + name, StringComparison.Ordinal));
    }
}
