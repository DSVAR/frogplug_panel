using FrogPlug.Agent.Services;

namespace FrogPlug.Agent.Endpoints;

public static class GeoEndpoints
{
    public static void MapGeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/geo/update", async (GeoUpdater updater, CancellationToken token) =>
        {
            var success = await updater.UpdateAsync(token);
            return success
                ? Results.Ok(new { ok = true })
                : Results.BadRequest(new { error = "Проверка geo-файлов не пройдена" });
        });
    }
}
