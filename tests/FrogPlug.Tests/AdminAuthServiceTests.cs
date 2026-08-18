using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using FrogPlug.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace FrogPlug.Tests;

public sealed class AdminAuthServiceTests
{
    [Fact]
    public async Task CreateFirstAdmin_creates_only_one_admin()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.Service;
        await service.CreateFirstAdmin("admin", "correct-password");
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFirstAdmin("second", "correct-password"));
        Assert.Equal(1, await fixture.Db.Admins.CountAsync());
    }

    [Fact]
    public async Task Login_succeeds_with_correct_password()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        var result = await fixture.Service.ValidateLogin("admin", "correct-password");
        Assert.True(result.Success);
        Assert.Equal("admin", result.Admin!.Login);
    }

    [Fact]
    public async Task Login_rejects_wrong_password()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        var result = await fixture.Service.ValidateLogin("admin", "wrong-password");
        Assert.False(result.Success);
        Assert.Equal("Неверный логин или пароль", result.Error);
    }

    [Fact]
    public async Task Login_rejects_unknown_login()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.ValidateLogin("missing", "wrong-password");
        Assert.False(result.Success);
        Assert.Equal("Неверный логин или пароль", result.Error);
    }

    [Fact]
    public async Task Login_locks_after_five_failures()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        for (var i = 0; i < 5; i++)
        {
            await fixture.Service.ValidateLogin("admin", "wrong-password");
        }

        var result = await fixture.Service.ValidateLogin("admin", "correct-password");
        Assert.False(result.Success);
        Assert.NotNull(result.LockedUntil);
    }

    [Fact]
    public async Task Login_lock_expires_after_fifteen_minutes()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        for (var i = 0; i < 5; i++)
        {
            await fixture.Service.ValidateLogin("admin", "wrong-password");
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(15));
        var result = await fixture.Service.ValidateLogin("admin", "correct-password");
        Assert.True(result.Success);
    }

    [Fact]
    public async Task Password_shorter_than_ten_is_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.CreateFirstAdmin("admin", "short"));
    }

    [Fact]
    public async Task ChangePassword_requires_old_password()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        Assert.False(await fixture.Service.ChangePassword(admin, "wrong-password", "new-password"));
        Assert.True((await fixture.Service.ValidateLogin("admin", "correct-password")).Success);
    }

    [Fact]
    public async Task ChangePassword_updates_password_when_old_password_matches()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        Assert.True(await fixture.Service.ChangePassword(admin, "correct-password", "new-password"));
        Assert.True((await fixture.Service.ValidateLogin("admin", "new-password")).Success);
    }

    [Fact]
    public async Task Enable_two_factor_rejects_wrong_code()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        Assert.False(await fixture.Service.EnableTwoFactor(admin, Totp.GenerateSecret(), "000000"));
        Assert.Null(admin.TotpSecret);
    }

    [Fact]
    public async Task Login_with_two_factor_requires_code_and_rejects_replay()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        var secret = Totp.GenerateSecret();
        var code = Totp.Generate(secret, fixture.Clock.GetUtcNow());
        Assert.True(await fixture.Service.EnableTwoFactor(admin, secret, code));
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));

        var passwordResult = await fixture.Service.ValidateLogin("admin", "correct-password");
        Assert.True(passwordResult.RequiresTwoFactor);
        var loginCode = Totp.Generate(secret, fixture.Clock.GetUtcNow());
        Assert.True((await fixture.Service.ValidateTwoFactor(admin.Id, loginCode)).Success);
        Assert.False((await fixture.Service.ValidateTwoFactor(admin.Id, loginCode)).Success);
    }

    [Fact]
    public async Task Two_factor_failures_lock_account_after_five_attempts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var admin = await fixture.Service.CreateFirstAdmin("admin", "correct-password");
        var secret = Totp.GenerateSecret();
        Assert.True(await fixture.Service.EnableTwoFactor(admin, secret, Totp.Generate(secret, fixture.Clock.GetUtcNow())));
        fixture.Clock.Advance(TimeSpan.FromSeconds(30));
        for (var index = 0; index < 5; index++)
        {
            await fixture.Service.ValidateTwoFactor(admin.Id, "000000");
        }

        Assert.NotNull((await fixture.Service.ValidateTwoFactor(admin.Id, "000000")).LockedUntil);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }
        public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);
        public AdminAuthService Service { get; }

        private Fixture(SqliteConnection connection, AppDbContext db)
        {
            this.connection = connection;
            Db = db;
            Service = new AdminAuthService(db, Clock);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, db);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
