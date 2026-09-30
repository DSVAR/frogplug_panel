using System.ComponentModel.DataAnnotations;

namespace FrogPlug.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task Defaults_are_returned()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        Assert.Equal("VPN", await fixture.Settings.GetProfileTitle());
        Assert.Equal(12, await fixture.Settings.GetSubscriptionUpdateHours());
    }

    [Fact]
    public async Task Values_can_be_saved_and_read()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await fixture.Settings.SetProfileTitle("Панель");
        await fixture.Settings.SetPublicBaseUrl("https://example.com");
        Assert.Equal("Панель", await fixture.Settings.GetProfileTitle());
        Assert.Equal("https://example.com", await fixture.Settings.GetPublicBaseUrl());
    }

    [Fact]
    public async Task Subscription_hours_range_is_validated()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Settings.SetSubscriptionUpdateHours(0));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Settings.SetSubscriptionUpdateHours(169));
    }

    [Fact]
    public async Task Optional_settings_default_to_null()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        Assert.Null(await fixture.Settings.GetSupportUrl());
        Assert.Null(await fixture.Settings.GetTelegramBotToken());
        Assert.Null(await fixture.Settings.GetTelegramChatId());
    }
}
