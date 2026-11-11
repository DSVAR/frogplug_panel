namespace FrogPlug.Core;

public static class MtProxyConfigBuilder
{
    // MTProxy для Telegram Web.
    public static string BuildStartScript(string secret)
    {
        var normalizedSecret = TgWebLinks.NormalizeSecret(secret);
        // --address не задаём: 127.0.0.1 ломает исходящие подключения к Telegram.
        return "#!/bin/sh\n" +
            "exec /usr/local/bin/mtproto-proxy \\\n" +
            "    -u frogplug \\\n" +
            $"    -p {NodePorts.TelegramWebMtproxyStats} \\\n" +
            $"    -H {NodePorts.TelegramWebMtproxy} \\\n" +
            $"    -S {normalizedSecret} \\\n" +
            "    --aes-pwd /etc/mtproxy/proxy-secret /etc/mtproxy/proxy-multi.conf \\\n" +
            "    -M 1\n";
    }
}
