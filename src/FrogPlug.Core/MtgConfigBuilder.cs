namespace FrogPlug.Core;

public static class MtgConfigBuilder
{
    // Настройка MTProto FakeTLS.
    public static string Build(string secret, int port, int frontingPort, string? frontingHost = null)
    {
        MtprotoSecret.Parse(secret);
        ValidatePort(port, nameof(port));
        ValidatePort(frontingPort, nameof(frontingPort));

        // В mtg адрес подмены задаётся через host, а не ip; Caddy принимает PROXY protocol.
        var host = frontingHost ?? MtprotoSecret.Parse(secret);
        var config = $"secret = \"{secret}\"\n" +
            $"bind-to = \"127.0.0.1:{port}\"\n" +
            "proxy-protocol-listener = true\n\n" +
            "[domain-fronting]\n" +
            $"host = \"{host}\"\n" +
            $"port = {frontingPort}\n";
        return host == "127.0.0.1" ? config + "proxy-protocol = true\n" : config;
    }

    private static void ValidatePort(int port, string name)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
