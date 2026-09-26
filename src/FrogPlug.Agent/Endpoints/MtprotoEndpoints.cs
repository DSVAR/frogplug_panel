using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class MtprotoEndpoints
{
    public static void MapMtprotoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/mtproto/apply", ApplyAsync);
    }

    private static async Task<IResult> ApplyAsync(
        MtprotoApplyRequest request,
        MtgManager manager,
        CancellationToken token)
    {
        var result = await manager.ApplyAsync(request, token);
        return result.Success ? Results.Ok() : Results.BadRequest(new { error = result.Error });
    }
}
