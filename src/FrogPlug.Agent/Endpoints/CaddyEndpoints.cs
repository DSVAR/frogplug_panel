using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class CaddyEndpoints
{
    public static void MapCaddyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/caddy/apply", async (CaddyApplyRequest request, CaddyManager manager, CancellationToken token) =>
        {
            var result = await manager.ApplyAsync(request, token);
            return result.Success ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error = result.Error });
        });
    }
}
