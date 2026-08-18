using FrogPlug.Server.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Time.Testing;

namespace FrogPlug.Tests;

public sealed class PendingLoginTokenServiceTests
{
    [Fact]
    public void Token_round_trip_preserves_login_data()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = new PendingLoginTokenService(CreateProvider("pending-login-tests"), clock);
        var token = service.Create(42, true);
        Assert.True(service.TryRead(token, out var login));
        Assert.Equal(42, login.AdminId);
        Assert.True(login.Remember);
    }

    [Fact]
    public void Token_expires_after_five_minutes()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var service = new PendingLoginTokenService(CreateProvider("pending-login-expiry"), clock);
        var token = service.Create(1, false);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.False(service.TryRead(token, out _));
    }

    private static IDataProtectionProvider CreateProvider(string name)
    {
        return DataProtectionProvider.Create(
            new DirectoryInfo(Path.GetTempPath()),
            builder => builder.SetApplicationName(name));
    }
}
