using FrogPlug.Server.Cli;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Tests;

public sealed class ResetAdminCommandTests
{
    [Fact]
    public async Task Unknown_login_returns_one()
    {
        await using var fixture = await Fixture.CreateAsync();
        var output = new StringWriter();
        var exitCode = await ResetAdminCommand.ExecuteAsync(fixture.Db, ["reset-admin", "missing", "correct-password"], output);
        Assert.Equal(1, exitCode);
        Assert.Contains("не найден", output.ToString());
    }

    [Fact]
    public async Task Success_resets_password_and_clears_two_factor()
    {
        await using var fixture = await Fixture.CreateAsync();
        var auth = new AdminAuthService(fixture.Db);
        var admin = await auth.CreateFirstAdmin("admin", "correct-password");
        admin.TotpSecret = "JBSWY3DPEHPK3PXP";
        admin.LastTotpStep = 123;
        admin.LockedUntil = DateTime.UtcNow.AddHours(1);
        await fixture.Db.SaveChangesAsync();

        var exitCode = await ResetAdminCommand.ExecuteAsync(fixture.Db, ["reset-admin", "admin", "new-correct-password"]);
        Assert.Equal(0, exitCode);
        Assert.Null(admin.TotpSecret);
        Assert.Null(admin.LastTotpStep);
        Assert.Null(admin.LockedUntil);
        Assert.True((await auth.ValidateLogin("admin", "new-correct-password")).Success);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public AppDbContext Db { get; }

        private Fixture(SqliteConnection connection, AppDbContext db)
        {
            this.connection = connection;
            Db = db;
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
