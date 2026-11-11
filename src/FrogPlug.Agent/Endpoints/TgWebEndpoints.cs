using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class TgWebEndpoints
{
    public static void MapTgWebEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/tgweb/apply", async (TgWebApplyRequest request, TgWebManager manager, CancellationToken token) =>
        {
            var result = await manager.ApplyAsync(request, token);
            return result.Success ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error = result.Error });
        });
    }
}
