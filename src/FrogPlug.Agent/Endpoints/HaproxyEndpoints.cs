using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class HaproxyEndpoints
{
    public static void MapHaproxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/haproxy/apply", async (HaproxyApplyRequest request, HaproxyManager manager, CancellationToken token) =>
        {
            var result = await manager.ApplyAsync(request, token);
            return result.Success ? Results.Ok() : Results.BadRequest(new { error = result.Error });
        });
    }
}
