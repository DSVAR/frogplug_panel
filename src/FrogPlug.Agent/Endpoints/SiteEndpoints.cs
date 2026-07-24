using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class SiteEndpoints
{
    public static void MapSiteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/site/apply", async (SiteApplyRequest request, SiteManager manager, CancellationToken token) =>
        {
            await manager.ApplyAsync(request, token);
            return Results.Ok();
        });
    }
}
