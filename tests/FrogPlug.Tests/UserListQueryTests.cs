using FrogPlug.Server.Data;
using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class UserListQueryTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Apply_filters_expiring_users_for_the_next_seven_days()
    {
        var users = new[]
        {
            User("Soon", expiresAt: Now.AddDays(7)),
            User("Later", expiresAt: Now.AddDays(8)),
            User("Expired", expiresAt: Now.AddMinutes(-1)),
            User("Disabled", enabled: false, expiresAt: Now.AddDays(2))
        };

        var result = UserListQuery.Apply(users, null, "expiring", 1, 10, Now);

        Assert.Equal(new[] { "Disabled", "Soon" }, result.Items.Select(user => user.Name));
    }

    [Fact]
    public void Apply_searches_name_and_note_case_insensitively()
    {
        var users = new[]
        {
            User("Алиса", note: "Тестовый доступ"),
            User("Боб", note: "Рабочий")
        };

        var byName = UserListQuery.Apply(users, "АЛИС", null, 1, 10, Now);
        var byNote = UserListQuery.Apply(users, "тестов", null, 1, 10, Now);

        Assert.Single(byName.Items);
        Assert.Single(byNote.Items);
        Assert.Equal("Алиса", byNote.Items[0].Name);
    }

    [Fact]
    public void Apply_clamps_invalid_page_and_page_size()
    {
        var users = Enumerable.Range(1, 11).Select(index => User($"User {index:00}")).ToList();

        var result = UserListQuery.Apply(users, null, null, 99, 99, Now);

        Assert.Equal(10, result.PageSize);
        Assert.Equal(2, result.Page);
        Assert.Single(result.Items);
        Assert.Equal("User 11", result.Items[0].Name);
    }

    [Fact]
    public void Apply_filters_users_by_active_status()
    {
        var users = new[]
        {
            User("Active"),
            User("Disabled", enabled: false),
            User("Expired", expiresAt: Now.AddMinutes(-1))
        };

        var result = UserListQuery.Apply(users, null, "Active", 1, 10, Now);

        Assert.Equal(new[] { "Active" }, result.Items.Select(user => user.Name));
    }

    [Fact]
    public void Apply_clamps_page_below_first_page()
    {
        var users = new[] { User("First"), User("Second") };

        var result = UserListQuery.Apply(users, null, null, 0, 10, Now);

        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.Items.Count);
    }

    private static ClientUser User(
        string name,
        bool enabled = true,
        string? note = null,
        DateTime? expiresAt = null)
    {
        return new ClientUser
        {
            Name = name,
            Note = note,
            IsEnabled = enabled,
            ExpiresAt = expiresAt,
            SubscriptionToken = "token"
        };
    }
}
