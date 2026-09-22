using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class OpenFluxEndpoints
{
    public static void MapOpenFluxEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/openflux/apply", ApplyAsync);
        app.MapPost("/openflux/remove", RemoveAsync);
    }

    private static async Task<IResult> ApplyAsync(OpenFluxApplyRequest request, OpenFluxManager manager, CancellationToken token)
    {
        var result = await manager.ApplyAsync(request, token);
        return result.Success ? Results.Ok(new { output = result.Output }) : Results.BadRequest(new { error = result.Error });
    }

    private static async Task<IResult> RemoveAsync(OpenFluxRemoveRequest request, OpenFluxManager manager, CancellationToken token)
    {
        var result = await manager.RemoveAsync(request.Channel, token);
        return result.Success ? Results.Ok(new { output = result.Output }) : Results.BadRequest(new { error = result.Error });
    }
}
