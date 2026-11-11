using System.Text;

namespace FrogPlug.Core;

public sealed record HysteriaUser(string Username, string Password);

public static class HysteriaConfigBuilder
{
    // Настройка Hysteria2.
    public static string Build(
        string certificate,
        string key,
        int port,
        IReadOnlyList<HysteriaUser> users,
        string? obfsPassword = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"listen: :{port}");
        builder.AppendLine("tls:");
        builder.AppendLine($"  cert: {Quote(certificate)}");
        builder.AppendLine($"  key: {Quote(key)}");
        builder.AppendLine("auth:");
        builder.AppendLine("  type: userpass");
        builder.AppendLine("  userpass:");
        foreach (var user in users.OrderBy(item => item.Username, StringComparer.Ordinal))
        {
            builder.AppendLine($"    {Quote(user.Username)}: {Quote(user.Password)}");
        }
        if (!string.IsNullOrEmpty(obfsPassword))
        {
            // Маскировка salamander заменяет внешнюю HTTP/3-маскировку.
            builder.AppendLine("obfs:");
            builder.AppendLine("  type: salamander");
            builder.AppendLine("  salamander:");
            builder.AppendLine($"    password: {Quote(obfsPassword)}");
            return builder.ToString();
        }

        // Без salamander используется сайт-заглушка.
        builder.AppendLine("masquerade:");
        builder.AppendLine("  type: file");
        builder.AppendLine("  file:");
        builder.AppendLine("    dir: /var/lib/frogplug/site");
        return builder.ToString();
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
    }
}
