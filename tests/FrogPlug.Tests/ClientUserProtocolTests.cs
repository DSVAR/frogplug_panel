using System.ComponentModel.DataAnnotations;
using FrogPlug.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Tests;

public sealed class ClientUserProtocolTests
{
    [Fact]
    public async Task Disabled_protocols_are_saved_as_false()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var input = new ClientUserInput("alice", null, AllowReality: false, AllowXhttp: false, AllowMtproto: true);
        var created = await fixture.Clients.Create(input);
        fixture.Db.ChangeTracker.Clear();

        var stored = await fixture.Db.ClientUsers.SingleAsync(item => item.Id == created.Id);

        Assert.False(stored.AllowReality);
        Assert.False(stored.AllowXhttp);
        Assert.True(stored.AllowMtproto);
    }

    [Fact]
    public async Task Update_changes_protocols()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var created = await fixture.Clients.Create(new ClientUserInput("bob", null));
        var input = new ClientUserInput("bob", null, AllowReality: false, AllowXhttp: true, AllowWhitelist: true);
        await fixture.Clients.Update(created.Id, input);
        fixture.Db.ChangeTracker.Clear();

        var stored = await fixture.Db.ClientUsers.SingleAsync(item => item.Id == created.Id);

        Assert.False(stored.AllowReality);
        Assert.True(stored.AllowXhttp);
        Assert.True(stored.AllowWhitelist);
    }

    [Fact]
    public async Task User_without_protocols_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var input = new ClientUserInput("carol", null, AllowReality: false, AllowXhttp: false);

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Clients.Create(input));
    }
}
