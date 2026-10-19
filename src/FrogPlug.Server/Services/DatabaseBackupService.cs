using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FrogPlug.Server.Data;

namespace FrogPlug.Server.Services;

public sealed class DatabaseBackupService(AppDbContext db)
{
    public async Task<byte[]> Create(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(Path.GetTempPath(), $"frogplug-{Guid.NewGuid():N}.db");
        try
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
            await using var command = CreateVacuumCommand(path);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return await File.ReadAllBytesAsync(path, cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            File.Delete(path);
        }
    }

    private SqliteCommand CreateVacuumCommand(string path)
    {
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path;";
        command.Parameters.AddWithValue("$path", path);
        return command;
    }
}
