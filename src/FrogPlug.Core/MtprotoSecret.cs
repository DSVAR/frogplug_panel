using System.Security.Cryptography;
using System.Text;

namespace FrogPlug.Core;

public static class MtprotoSecret
{
    public static string Generate(string domain)
    {
        ValidateDomain(domain);
        var random = RandomNumberGenerator.GetBytes(16);
        var domainBytes = Encoding.UTF8.GetBytes(domain);
        return "ee" + Convert.ToHexString(random).ToLowerInvariant() +
            Convert.ToHexString(domainBytes).ToLowerInvariant();
    }

    public static string Parse(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) ||
            !secret.StartsWith("ee", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Секрет MTProto должен начинаться с ee", nameof(secret));
        }

        var value = secret[2..];
        if (value.Length < 34 || value.Length % 2 != 0 || !value.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Некорректный формат секрета MTProto", nameof(secret));
        }

        var domain = Encoding.UTF8.GetString(Convert.FromHexString(value[32..]));
        ValidateDomain(domain);
        return domain;
    }

    private static void ValidateDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain) ||
            domain.Any(char.IsWhiteSpace) ||
            domain.Contains('/') ||
            domain.Contains(':'))
        {
            throw new ArgumentException("Для MTProto нужен корректный домен ноды", nameof(domain));
        }
    }
}
