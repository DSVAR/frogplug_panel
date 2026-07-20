using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class XrayEndpoints
{
    public static void MapXrayEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/xray/config", ApplyConfigAsync);
        app.MapGet("/xray/stats", GetStatsAsync);
        app.MapPost("/xray/update", UpdateAsync);
    }

    private static async Task<IResult> ApplyConfigAsync(
        XrayConfigRequest request,
        XrayManager manager,
        GeoUpdater geoUpdater,
        CancellationToken token)
    {
        if (!await geoUpdater.EnsureAvailableAsync(token))
        {
            return Results.BadRequest(new { error = "Не удалось подготовить geo-файлы" });
        }

        var result = await manager.ApplyAsync(request.ConfigJson, token);
        return result.Success ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> GetStatsAsync(bool? reset, XrayManager manager, CancellationToken token)
    {
        return Results.Ok(await manager.GetStatsAsync(reset == true, token));
    }

    private static async Task<IResult> UpdateAsync(XrayUpdateRequest request, XrayUpdater updater, CancellationToken token)
    {
        var result = await updater.UpdateAsync(
            request.Version,
            request.Arch,
            request.DownloadUrl,
            request.Sha256,
            token);
        return result.Success
            ? Results.Ok(new { oldVersion = result.OldVersion, newVersion = result.NewVersion })
            : Results.BadRequest(new { error = result.Error });
    }
}
