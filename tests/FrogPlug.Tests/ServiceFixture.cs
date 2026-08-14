using Microsoft.AspNetCore.Http;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace FrogPlug.Tests;

public sealed class ServiceFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    public AppDbContext Db { get; }
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    public AuditService Audit { get; }
    public ClientUserService Clients { get; }
    public NodeService Nodes { get; }
    public RoutingService Routing { get; }
    public SettingsService Settings { get; }
    public DatabaseBackupService Backups { get; }

    private ServiceFixture(SqliteConnection connection, AppDbContext db)
    {
        this.connection = connection;
        Db = db;
        Audit = new AuditService(db, new HttpContextAccessor());
        Clients = new ClientUserService(db, Clock, Audit);
        Nodes = new NodeService(db, Clock, Audit);
        Routing = new RoutingService(db, Audit);
        Settings = new SettingsService(db);
        Backups = new DatabaseBackupService(db);
    }

    public static async Task<ServiceFixture> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return new ServiceFixture(connection, db);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await connection.DisposeAsync();
    }
}

