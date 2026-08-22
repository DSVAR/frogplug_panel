using System.ComponentModel.DataAnnotations;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class ClientUserServiceTests
{
    [Fact]
    public async Task Create_sets_defaults()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user = await fixture.Clients.Create(new ClientUserInput("alice", null));
        Assert.True(user.IsEnabled);
        Assert.NotEqual(Guid.Empty, user.VlessUuid);
        Assert.Equal(43, user.SubscriptionToken.Length);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime, user.CreatedAt);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_case_insensitively()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await fixture.Clients.Create(new ClientUserInput("Alice", null));
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Clients.Create(new ClientUserInput("alice", null)));
    }

    [Fact]
    public async Task Invalid_name_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => fixture.Clients.Create(new ClientUserInput("bad/@name", null)));
    }

    [Fact]
    public void Statuses_are_computed()
    {
        var now = new DateTime(2026, 1, 1);
        var user = NewUser();
        Assert.Equal(ClientStatus.Active, ClientUserService.GetStatus(user, now));
        user.IsEnabled = false;
        Assert.Equal(ClientStatus.Disabled, ClientUserService.GetStatus(user, now));
        user.IsEnabled = true;
        user.ExpiresAt = now.AddMinutes(-1);
        Assert.Equal(ClientStatus.Expired, ClientUserService.GetStatus(user, now));
        user.ExpiresAt = null;
        user.TrafficLimitBytes = 1;
        user.UsedUploadBytes = 1;
        Assert.Equal(ClientStatus.OverLimit, ClientUserService.GetStatus(user, now));
    }

    [Fact]
    public async Task ExtendDays_uses_now_when_expiry_is_past()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user = await fixture.Clients.Create(new ClientUserInput("alice", null, true, fixture.Clock.GetUtcNow().UtcDateTime.AddDays(-2)));
        await fixture.Clients.ExtendDays(user.Id, 3);
        Assert.Equal(fixture.Clock.GetUtcNow().UtcDateTime.AddDays(3), user.ExpiresAt);
    }

    [Fact]
    public async Task ExtendDays_uses_future_expiry()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var expiry = fixture.Clock.GetUtcNow().UtcDateTime.AddDays(5);
        var user = await fixture.Clients.Create(new ClientUserInput("alice", null, true, expiry));
        await fixture.Clients.ExtendDays(user.Id, 3);
        Assert.Equal(expiry.AddDays(3), user.ExpiresAt);
    }

    [Fact]
    public async Task RegenerateToken_changes_token()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user = await fixture.Clients.Create(new ClientUserInput("alice", null));
        var old = user.SubscriptionToken;
        await fixture.Clients.RegenerateToken(user.Id);
        Assert.NotEqual(old, user.SubscriptionToken);
    }

    [Fact]
    public async Task ResetTraffic_clears_both_counters()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user = await fixture.Clients.Create(new ClientUserInput("alice", null));
        user.UsedUploadBytes = 10;
        user.UsedDownloadBytes = 20;
        await fixture.Db.SaveChangesAsync();
        await fixture.Clients.ResetTraffic(user.Id);
        Assert.Equal(0, user.UsedUploadBytes + user.UsedDownloadBytes);
    }

    private static ClientUser NewUser()
    {
        return new ClientUser
        {
            Name = "user",
            SubscriptionToken = "token"
        };
    }
}
