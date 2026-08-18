using System.Security.Cryptography;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed class FirstSetupService(AppDbContext db, TimeProvider timeProvider)
{
    private const string TokenHashKey = "FirstSetupTokenHash";
    private const string TokenCreatedAtKey = "FirstSetupTokenCreatedAt";
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(24);

    public async Task<string?> CreateIfNeeded()
    {
        if (await db.Admins.AnyAsync() || await HasValidToken())
        {
            return null;
        }

        return await CreateFresh();
    }

    public async Task<string> CreateFresh()
    {
        var token = SecureTokens.RandomBase64Url(32);
        await Set(TokenHashKey, Hash(token));
        await Set(TokenCreatedAtKey, timeProvider.GetUtcNow().ToString("O"));
        return token;
    }

    public async Task<bool> IsValid(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !await HasValidToken())
        {
            return false;
        }

        var storedHash = await Get(TokenHashKey);
        return storedHash is not null && CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(storedHash),
            Convert.FromBase64String(Hash(token)));
    }

    public async Task Consume()
    {
        db.Settings.RemoveRange(db.Settings.Where(item => item.Key == TokenHashKey || item.Key == TokenCreatedAtKey));
        await db.SaveChangesAsync();
    }

    public async Task<string> BuildLink(string token, string? host = null)
    {
        var baseUrl = await Get("PublicBaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = string.IsNullOrWhiteSpace(host)
                ? $"http://{PublicIpv4() ?? Environment.MachineName}"
                : host.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? host
                    : $"http://{host}";
        }

        return $"{baseUrl.TrimEnd('/')}/setup?token={Uri.EscapeDataString(token)}";
    }

    private async Task<bool> HasValidToken()
    {
        var createdAt = await Get(TokenCreatedAtKey);
        if (!DateTimeOffset.TryParse(createdAt, out var created))
        {
            return false;
        }

        return created.Add(TokenLifetime) > timeProvider.GetUtcNow() && await Get(TokenHashKey) is not null;
    }

    private Task<string?> Get(string key)
    {
        return db.Settings.Where(item => item.Key == key).Select(item => item.Value).FirstOrDefaultAsync();
    }

    private async Task Set(string key, string value)
    {
        var setting = await db.Settings.FindAsync(key);
        if (setting is null)
        {
            db.Settings.Add(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync();
    }

    private static string Hash(string token)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    public static string? PublicIpv4()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(item => item.OperationalStatus == OperationalStatus.Up)
            .SelectMany(item => item.GetIPProperties().UnicastAddresses)
            .Select(item => item.Address)
            .FirstOrDefault(IsPublicIpv4)
            ?.ToString();
    }

    private static bool IsPublicIpv4(IPAddress address)
    {
        if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return !IPAddress.IsLoopback(address)
            && bytes[0] != 10
            && (bytes[0] != 172 || bytes[1] is < 16 or > 31)
            && (bytes[0] != 192 || bytes[1] != 168)
            && bytes[0] != 127
            && bytes[0] != 169;
    }
}
