using FrogPlug.Agent.Services;
using FrogPlug.Core;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace FrogPlug.Agent.Endpoints;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/status", async (
            SystemInfo info,
            IFileSystem files,
            ICommandRunner runner,
            IServiceController services,
            OpenFluxManager openFlux,
            MtgManager mtg,
            IOptions<AgentOptions> options,
            CancellationToken token) =>
        {
            var system = await info.ReadAsync(files, token);
            var xray = await runner.RunAsync(options.Value.XrayBinary, ["version"], TimeSpan.FromSeconds(10), token);
            var active = await services.IsActiveAsync("xray", token);
            return Results.Ok(new AgentStatus(
                typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                system.Hostname,
                system.UptimeSeconds,
                system.LoadAverage,
                system.MemoryBytes,
                system.DiskFreeBytes,
                new ComponentStatus(xray.ExitCode == 0, active.ExitCode == 0, xray.StandardOutput.Split('\n').FirstOrDefault()),
                await mtg.GetStatusAsync(token),
                new ComponentStatus(false, false),
                await openFlux.GetStatusAsync(token),
                Architecture: RuntimeInformation.ProcessArchitecture.ToString()));
        });
    }
}
