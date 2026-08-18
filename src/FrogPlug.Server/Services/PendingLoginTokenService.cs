using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace FrogPlug.Server.Services;

public sealed class PendingLoginTokenService
{
    private readonly IDataProtector protector;
    private readonly TimeProvider clock;

    public PendingLoginTokenService(IDataProtectionProvider provider, TimeProvider? timeProvider = null)
    {
        protector = provider.CreateProtector("FrogPlug.PendingLogin.v1");
        clock = timeProvider ?? TimeProvider.System;
    }

    public string Create(int adminId, bool remember)
    {
        var expiresAt = clock.GetUtcNow().AddMinutes(5).UtcDateTime;
        var value = string.Create(CultureInfo.InvariantCulture, $"{adminId}|{remember}|{expiresAt.Ticks}");
        return protector.Protect(value);
    }

    public bool TryRead(string? token, out PendingLogin pendingLogin)
    {
        pendingLogin = default!;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var parts = protector.Unprotect(token).Split('|');
            if (parts.Length != 3
                || !int.TryParse(parts[0], CultureInfo.InvariantCulture, out var adminId)
                || !bool.TryParse(parts[1], out var remember)
                || !long.TryParse(parts[2], CultureInfo.InvariantCulture, out var ticks))
            {
                return false;
            }

            var expiresAt = new DateTime(ticks, DateTimeKind.Utc);
            if (expiresAt <= clock.GetUtcNow().UtcDateTime)
            {
                return false;
            }

            pendingLogin = new PendingLogin(adminId, remember, expiresAt);
            return true;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
