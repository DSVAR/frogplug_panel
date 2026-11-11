namespace FrogPlug.Core;

public static class HysteriaLinks
{
    public static string Build(
        string username,
        string password,
        string host,
        int port,
        string name,
        string? obfsPassword = null,
        string? pinSha256 = null)
    {
        // Двоеточие между логином и паролем оставляем как есть: часть клиентов (Happ) не раскодирует %3A.
        var auth = $"{Uri.EscapeDataString(username)}:{Uri.EscapeDataString(password)}";
        var query = $"sni={Uri.EscapeDataString(host)}";
        if (!string.IsNullOrEmpty(obfsPassword))
        {
            query += $"&obfs=salamander&obfs-password={Uri.EscapeDataString(obfsPassword)}";
        }
        if (!string.IsNullOrWhiteSpace(pinSha256))
        {
            query += $"&pinSHA256={Uri.EscapeDataString(pinSha256)}";
        }
        return $"hysteria2://{auth}@{host}:{port}/?{query}#{Uri.EscapeDataString(name)}";
    }

    // Провайдеры в РФ режут QUIC по сигнатуре: salamander превращает пакеты в шум.
    // Пароль выводим из секрета ноды, чтобы не хранить ещё одно поле.
    public static string ObfsPassword(Guid nodeSecret)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"hysteria-obfs:{nodeSecret:N}"));
        return Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
    }
}
