using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class HysteriaEndpoints
{
    public static void MapHysteriaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/hysteria/apply", ApplyAsync);
    }

    private static async Task<IResult> ApplyAsync(
        HysteriaApplyRequest request,
        HysteriaManager manager,
        CancellationToken token)
    {
        var result = await manager.ApplyAsync(request, token);
        return result.Success ? Results.Ok() : Results.BadRequest(new { error = result.Error });
    }
}
