using System.Security.Claims;
using FrogPlug.Server.Components;
using FrogPlug.Server.Cli;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using FrogPlug.Server.Components.Shared;
using FrogPlug.Server.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
// Секретный путь панели.
var configuredPanelPath = builder.Configuration["PanelPath"]?.Trim();
if (!string.IsNullOrWhiteSpace(configuredPanelPath) &&
    (configuredPanelPath == "/" || !configuredPanelPath.StartsWith('/')))
{
    throw new InvalidOperationException("PanelPath должен начинаться с '/' и отличаться от '/'.");
}
var panelPath = string.IsNullOrWhiteSpace(configuredPanelPath)
    ? string.Empty
    : configuredPanelPath.TrimEnd('/');
// Путь подписок.
var configuredSubscriptionPath = builder.Configuration["SubscriptionPath"]?.Trim();
if (!string.IsNullOrWhiteSpace(configuredSubscriptionPath) &&
    (configuredSubscriptionPath == "/" || !configuredSubscriptionPath.StartsWith('/')))
{
    throw new InvalidOperationException("SubscriptionPath должен начинаться с '/' и отличаться от '/'.");
}
var subscriptionPath = string.IsNullOrWhiteSpace(configuredSubscriptionPath)
    ? string.Empty
    : configuredSubscriptionPath.TrimEnd('/');
if (!string.IsNullOrEmpty(subscriptionPath) &&
    (!string.IsNullOrEmpty(panelPath) &&
     (subscriptionPath == panelPath || subscriptionPath.StartsWith(panelPath + "/", StringComparison.Ordinal) ||
      panelPath.StartsWith(subscriptionPath + "/", StringComparison.Ordinal))))
{
    throw new InvalidOperationException("SubscriptionPath не должен совпадать с PanelPath или быть его префиксом.");
}
var dataDirectory = builder.Configuration["DataDirectory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "appdata");
Directory.CreateDirectory(dataDirectory);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? $"Data Source={Path.Combine(dataDirectory, "frogplug.db")}";
var keysDirectory = Path.Combine(dataDirectory, "keys");
Directory.CreateDirectory(keysDirectory);

if (ResetAdminCommand.IsCommand(args) || SetupLinkCommand.IsCommand(args) || AddLocalNodeCommand.IsCommand(args))
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connectionString)
        .Options;
    await using var db = new AppDbContext(options);
    await db.Database.MigrateAsync();
    Environment.ExitCode = ResetAdminCommand.IsCommand(args)
        ? await ResetAdminCommand.ExecuteAsync(db, args)
        : SetupLinkCommand.IsCommand(args)
            ? await SetupLinkCommand.ExecuteAsync(db, builder.Configuration["PublicBaseUrl"])
            : await AddLocalNodeCommand.ExecuteAsync(db, args);
    return;
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDirectory))
    .SetApplicationName("FrogPlug");
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AgentTunnelRegistry>();
builder.Services.AddSingleton<XrayCoreStore>();
builder.Services.AddScoped<ITelegramNotifier, TelegramNotifier>();
builder.Services.AddScoped<AgentClient>();
builder.Services.AddScoped<IAgentClient>(services => services.GetRequiredService<AgentClient>());
builder.Services.AddScoped<NodeSyncService>();
builder.Services.AddSingleton<SyncQueue>();
builder.Services.AddHostedService<SyncWorker>();
builder.Services.AddScoped<NodeMonitor>();
builder.Services.AddHostedService<NodeMonitorWorker>();
builder.Services.AddSingleton<XrayReleaseService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<FirstSetupService>();
builder.Services.AddScoped<DatabaseBackupService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<OpenFluxService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<PendingLoginTokenService>();
builder.Services.AddScoped<ClientUserService>();
builder.Services.AddScoped<NodeService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddSingleton(new PublicLinkOptions(panelPath, subscriptionPath));
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("public", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.Cookie.Path = string.IsNullOrEmpty(panelPath) ? "/" : panelPath;
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

var app = builder.Build();

// Доверенные прокси.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Add(IPAddress.Loopback);
forwardedHeaders.KnownProxies.Add(IPAddress.IPv6Loopback);
// В Docker прокси (Caddy на хосте) приходит из сети моста, а не с loopback — её задаём явно.
foreach (var network in (builder.Configuration["TrustedProxyNetworks"] ?? string.Empty)
    .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries))
{
    forwardedHeaders.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
}

app.UseForwardedHeaders(forwardedHeaders);

if (!string.IsNullOrEmpty(subscriptionPath))
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.Path.StartsWithSegments(subscriptionPath))
        {
            await next();
            return;
        }

        context.Request.PathBase = subscriptionPath;
        context.Request.Path = context.Request.Path.Value![subscriptionPath.Length..];
        if (!IsSubscriptionRequestAllowed(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
}
if (!string.IsNullOrEmpty(panelPath))
{
    app.Use(async (context, next) =>
    {
        // Пустой subscriptionPath совпал бы с пустым PathBase и открыл бы панель без секретного пути.
        if (!string.IsNullOrEmpty(subscriptionPath) && context.Request.PathBase == subscriptionPath)
        {
            await next();
            return;
        }

        if (!context.Request.Path.StartsWithSegments(panelPath))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await next();
    });
    app.UsePathBase(panelPath);
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    scope.ServiceProvider.GetRequiredService<RoutingService>().SeedDefaultsAsync().GetAwaiter().GetResult();
    var setup = scope.ServiceProvider.GetRequiredService<FirstSetupService>();
    var token = setup.CreateIfNeeded().GetAwaiter().GetResult();
    if (token is not null)
    {
        var link = setup.BuildLink(token, builder.Configuration["PublicBaseUrl"]).GetAwaiter().GetResult();
        app.Logger.LogInformation("Первая настройка: {Link}", link);
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseAuthentication();
app.UseRateLimiter();
app.UseWebSockets();
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isFirstSetupPublic = path == "/setup" || path == "/login" || path == "/health"
        || path.StartsWithSegments("/sub") || path.StartsWithSegments("/u")
        || path.StartsWithSegments("/agent")
        || path.StartsWithSegments("/_")
        || (path.HasValue && Path.HasExtension(path.Value));
    if (!isFirstSetupPublic)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Admins.AnyAsync())
        {
            context.Response.Redirect($"{context.Request.PathBase}/setup");
            return;
        }
    }

    await next();
});
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAgentTunnelEndpoint();
app.MapAgentDownloadEndpoints(app.Environment, app.Configuration);
app.MapGet("/sub/{token}", async (string token, SubscriptionService subscriptions, HttpContext context) =>
{
    if (IsOpenFluxRequest(context.Request))
    {
        var transports = OpenFluxSubscriptionRequest.Transports(context.Request);
        var openFlux = await subscriptions.BuildOpenFlux(token, transports);
        if (openFlux.User is null)
        {
            return Results.NotFound();
        }

        return Results.Text(openFlux.Body, "application/json; charset=utf-8");
    }

    var result = await subscriptions.Build(token);
    if (result.User is null)
    {
        return Results.NotFound();
    }

    var title = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(result.ProfileTitle));
    context.Response.Headers["profile-title"] = $"base64:{title}";
    var total = result.User.TrafficLimitBytes ?? 0;
    var expire = result.User.ExpiresAt is { } expires ? new DateTimeOffset(expires).ToUnixTimeSeconds() : 0;
    context.Response.Headers["subscription-userinfo"] =
        $"upload={result.User.UsedUploadBytes}; download={result.User.UsedDownloadBytes}; total={total}; expire={expire}";
    context.Response.Headers["profile-update-interval"] = result.UpdateHours.ToString();
    if (!string.IsNullOrWhiteSpace(result.SupportUrl))
    {
        context.Response.Headers["support-url"] = result.SupportUrl;
    }
    var baseUrl = result.PublicBaseUrl ?? $"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}";
    baseUrl = app.Services.GetRequiredService<PublicLinkOptions>().GetSubscriptionBaseUrl(baseUrl);
    context.Response.Headers["profile-web-page-url"] = $"{baseUrl.TrimEnd('/')}/u/{token}";
    context.Response.Headers["content-disposition"] = $"attachment; filename=\"{result.ProfileTitle}\"";
    return Results.Text(result.Body, "text/plain; charset=utf-8");
}).AllowAnonymous().RequireRateLimiting("public");
app.MapPost("/logout", async (HttpContext context, AuditService audit) =>
{
    var login = context.User.Identity?.Name ?? "неизвестный";
    await audit.Write(AuditActions.Logout, null, login);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("login");
}).AllowAnonymous();
app.MapPost("/admins/deactivate", async ([Microsoft.AspNetCore.Mvc.FromForm] int id, HttpContext context, AdminAuthService auth) =>
{
    var actor = context.User.Identity?.Name ?? string.Empty;
    await auth.DeactivateAdmin(id, actor);
    return Results.Redirect($"{context.Request.PathBase}/admins");
});
app.MapBackupEndpoint();
app.MapStaticAssets().AllowAnonymous();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsOpenFluxRequest(HttpRequest request)
{
    return request.Headers.ContainsKey(OpenFluxSubscriptionRequest.TransportsHeader)
        || request.Headers.UserAgent.ToString().Contains("OpenFlux", StringComparison.OrdinalIgnoreCase);
}

static bool IsSubscriptionRequestAllowed(PathString path)
{
    var value = path.Value ?? string.Empty;
    return value.StartsWith("/sub/", StringComparison.Ordinal) ||
        value.StartsWith("/u/", StringComparison.Ordinal) ||
        value.StartsWith("/css/", StringComparison.Ordinal) ||
        value.StartsWith("/js/", StringComparison.Ordinal) ||
        value.StartsWith("/img/", StringComparison.Ordinal) ||
        value.StartsWith("/_framework/", StringComparison.Ordinal) ||
        value.StartsWith("/_content/", StringComparison.Ordinal) ||
        value.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
        // Скрипты компонентов разметки (ReconnectModal.*.razor.js) подключаются на каждой странице.
        (value.StartsWith("/Components/", StringComparison.Ordinal) && value.EndsWith(".razor.js", StringComparison.Ordinal));
}

public partial class Program;
