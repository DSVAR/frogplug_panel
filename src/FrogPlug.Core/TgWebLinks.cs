namespace FrogPlug.Core;

public static class TgWebLinks
{
    public static string Https(string domain, string secret)
    {
        secret = NormalizeSecret(secret);
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Домен обязателен", nameof(domain));
        }

        return $"https://t.me/webproxy?server={Uri.EscapeDataString(domain.Trim())}&secret={secret}";
    }

    public static string Tg(string domain, string secret)
    {
        secret = NormalizeSecret(secret);
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Домен обязателен", nameof(domain));
        }

        return $"tg://webproxy?server={Uri.EscapeDataString(domain.Trim())}&secret={secret}";
    }

    public static string NormalizeSecret(string secret)
    {
        MtprotoSecret.Parse(secret);
        return secret[2..34];
    }
}
