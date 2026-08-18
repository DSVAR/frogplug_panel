using FrogPlug.Core;
using FrogPlug.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Tests;

public sealed class FirstSetupServiceTests
{
    [Fact]
    public async Task Token_is_hashed_and_valid_until_expiration()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var service = new FirstSetupService(fixture.Db, fixture.Clock);
        var token = await service.CreateFresh();

        var hash = await fixture.Db.Settings
            .Where(item => item.Key == "FirstSetupTokenHash")
            .Select(item => item.Value)
            .SingleAsync();

        Assert.NotEqual(token, hash);
        Assert.True(await service.IsValid(token));
        fixture.Clock.Advance(TimeSpan.FromHours(24));
        Assert.False(await service.IsValid(token));
    }

    [Fact]
    public async Task Wizard_completion_creates_admin_and_consumes_token()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var service = new FirstSetupService(fixture.Db, fixture.Clock);
        var token = await service.CreateFresh();

        var auth = new AdminAuthService(fixture.Db, fixture.Clock, fixture.Audit);
        await auth.CreateFirstAdmin("admin", "correct-password");
        await service.Consume();

        Assert.False(await service.IsValid(token));
        Assert.Empty(await fixture.Db.Settings.ToListAsync());
        Assert.Single(await fixture.Db.Admins.ToListAsync());
    }

    [Fact]
    public async Task Expired_token_is_replaced_on_next_start()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var service = new FirstSetupService(fixture.Db, fixture.Clock);
        var expired = await service.CreateFresh();
        fixture.Clock.Advance(TimeSpan.FromHours(25));

        var replacement = await service.CreateIfNeeded();

        Assert.NotNull(replacement);
        Assert.NotEqual(expired, replacement);
        Assert.True(await service.IsValid(replacement));
    }

    [Fact]
    public void Password_generator_uses_unambiguous_random_characters()
    {
        var first = PasswordGenerator.Generate();
        var second = PasswordGenerator.Generate();

        Assert.Equal(20, first.Length);
        Assert.Matches("^[A-HJ-KM-NP-Zabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*\\-_=+]+$", first);
        Assert.DoesNotContain('l', first);
        Assert.DoesNotContain('1', first);
        Assert.DoesNotContain('O', first);
        Assert.DoesNotContain('0', first);
        Assert.NotEqual(first, second);
    }
}
