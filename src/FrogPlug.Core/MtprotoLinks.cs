namespace FrogPlug.Core;

public static class MtprotoLinks
{
    public static string Tg(string domain, int port, string secret)
    {
        return Build("tg://proxy", domain, port, secret);
    }

    public static string Tg(string domain, string secret)
    {
        return Tg(domain, NodePorts.Public, secret);
    }

    public static string Https(string domain, int port, string secret)
    {
        return Build("https://t.me/proxy", domain, port, secret);
    }

    public static string Https(string domain, string secret)
    {
        return Https(domain, NodePorts.Public, secret);
    }

    private static string Build(string prefix, string domain, int port, string secret)
    {
        MtprotoSecret.Parse(secret);
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        return $"{prefix}?server={Uri.EscapeDataString(domain)}&port={port}&secret={secret}";
    }
}
