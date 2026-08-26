using FrogPlug.Server.Services;

namespace FrogPlug.Server.Endpoints;

public static class AgentDownloadEndpoints
{
    public static IEndpointRouteBuilder MapAgentDownloadEndpoints(
        this IEndpointRouteBuilder endpoints,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        endpoints.MapGet("/agent/xray/default", (XrayCoreStore cores) =>
            Results.Text(cores.DefaultVersion, "text/plain; charset=utf-8"))
            .AllowAnonymous()
            .RequireRateLimiting("public");

        endpoints.MapGet("/agent/xray/{version}/{rid}.sha256", async (
            string version,
            string rid,
            XrayCoreStore cores,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var core = await cores.EnsureAsync(version, rid, cancellationToken);
                return Results.Text(core.Sha256, "text/plain; charset=utf-8");
            }
            catch (ArgumentException)
            {
                return Results.NotFound();
            }
        }).AllowAnonymous().RequireRateLimiting("public");

        endpoints.MapGet("/agent/xray/{version}/{rid}", async (
            string version,
            string rid,
            XrayCoreStore cores,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var core = await cores.EnsureAsync(version, rid, cancellationToken);
                return Results.File(core.Path, "application/zip", Path.GetFileName(core.Path));
            }
            catch (ArgumentException)
            {
                return Results.NotFound();
            }
        }).AllowAnonymous().RequireRateLimiting("public");

        endpoints.MapGet("/agent/download/{rid}", (string rid) =>
        {
            if (rid is not ("linux-x64" or "linux-arm64"))
            {
                return Results.NotFound();
            }

            var root = configuration["Agent:ArtifactsPath"] ??
                Path.Combine(environment.ContentRootPath, "artifacts", "agent");
            if (!Path.IsPathRooted(root))
            {
                root = Path.Combine(environment.ContentRootPath, root);
            }

            var path = Path.Combine(root, rid, "frogplug-agent");
            return File.Exists(path)
                ? Results.File(path, "application/octet-stream", "frogplug-agent")
                : Results.NotFound();
        }).AllowAnonymous();

        endpoints.MapGet("/agent/install.sh", () =>
        {
            var path = Path.Combine(environment.ContentRootPath, "deploy", "agent-install.sh");
            if (!File.Exists(path))
            {
                return Results.NotFound();
            }

            var text = File.ReadAllText(path).Replace(
                "${FROGPLUG_PANEL_URL:-}",
                configuration["PublicBaseUrl"] ?? string.Empty,
                StringComparison.Ordinal);
            return Results.Text(text, "text/x-shellscript; charset=utf-8");
        }).AllowAnonymous();
        return endpoints;
    }
}
