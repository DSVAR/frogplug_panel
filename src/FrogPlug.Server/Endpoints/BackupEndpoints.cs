using FrogPlug.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FrogPlug.Server.Endpoints;

public static class BackupEndpoints
{
    public static IEndpointConventionBuilder MapBackupEndpoint(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("/admin/backup", Download)
            .RequireAuthorization();
    }

    private static async Task<IResult> Download(
        DatabaseBackupService backups,
        AuditService audit,
        CancellationToken cancellationToken)
    {
        var contents = await backups.Create(cancellationToken);
        await audit.Write(AuditActions.BackupDownload);
        return Results.File(contents, "application/vnd.sqlite3", "frogplug-backup.db");
    }
}
