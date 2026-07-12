using FrogPlug.Agent;
using FrogPlug.Agent.Endpoints;
using FrogPlug.Agent.Services;

var builder = WebApplication.CreateBuilder(args);
var configPath = Environment.GetEnvironmentVariable("FROGPLUG_AGENT_CONFIG") ?? "/etc/frogplug-agent/agent.json";
builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: false);
builder.Services.Configure<AgentOptions>(builder.Configuration);
builder.Services.AddSingleton(sp => sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value);
builder.Services.AddSingleton<IFileSystem, PhysicalFileSystem>();
builder.Services.AddSingleton<ICommandRunner, ProcessCommandRunner>();
builder.Services.AddSingleton<IServiceController>(services =>
{
    var options = services.GetRequiredService<AgentOptions>();
    var runner = services.GetRequiredService<ICommandRunner>();
    return options.ServiceMode.ToLowerInvariant() switch
    {
        "docker" => new SupervisorServiceController(runner),
        "systemd" => new SystemdServiceController(runner),
        _ => throw new InvalidOperationException("Неизвестный режим управления службами.")
    };
});
builder.Services.AddHttpClient();
builder.Services.AddHostedService<TunnelClient>();
builder.Services.AddSingleton<XrayManager>();
builder.Services.AddSingleton<XrayUpdater>();
builder.Services.AddSingleton<GeoUpdater>();
builder.Services.AddSingleton<CaddyManager>();
builder.Services.AddSingleton<HaproxyManager>();
builder.Services.AddSingleton<SiteManager>();
builder.Services.AddSingleton<OpenFluxManager>();
builder.Services.AddSingleton<MtgManager>();
builder.Services.AddSingleton<TgWebManager>();
builder.Services.AddSingleton<HysteriaManager>();
builder.Services.AddSingleton<SystemInfo>();
builder.Services.AddSingleton<ISniNetwork, SniNetwork>();
builder.Services.AddSingleton<SniScanner>();
var options = builder.Configuration.Get<AgentOptions>() ?? new AgentOptions();
builder.WebHost.ConfigureKestrel(server => server.ConfigureEndpoint(options));
var app = builder.Build();
app.UseMiddleware<BearerAuthMiddleware>();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapStatusEndpoints();
app.MapXrayEndpoints();
app.MapGeoEndpoints();
app.MapCaddyEndpoints();
app.MapHaproxyEndpoints();
app.MapSiteEndpoints();
app.MapSniEndpoints();
app.MapOpenFluxEndpoints();
app.MapMtprotoEndpoints();
app.MapTgWebEndpoints();
app.MapHysteriaEndpoints();
app.Run();

public partial class Program
{
}
