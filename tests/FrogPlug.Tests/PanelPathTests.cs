using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FrogPlug.Tests;

public sealed class PanelPathTests : IAsyncLifetime
{
    private PanelPathWebApplicationFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new PanelPathWebApplicationFactory();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Panel_path_prefix_serves_login_assets_and_rejects_root_routes()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var login = await client.GetAsync("/k3x9q2m7w1v8z4tp/login");
        var loginHtml = await login.Content.ReadAsStringAsync();
        var rootLogin = await client.GetAsync("/login");
        var asset = await client.GetAsync("/k3x9q2m7w1v8z4tp/app.css");

        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
        Assert.Contains("<base href=\"/k3x9q2m7w1v8z4tp/\"", loginHtml);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, rootLogin.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, asset.StatusCode);
    }

    [Fact]
    public async Task Subscription_path_hides_panel_path_and_rejects_admin_routes()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var personal = await client.GetAsync("/connect/u/missing");
        var login = await client.GetAsync("/connect/login");
        var header = await client.GetAsync("/connect/sub/missing");
        var html = await personal.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, personal.StatusCode);
        Assert.Contains("<base href=\"/connect/\"", html);
        Assert.DoesNotContain("/k3x9q2m7w1v8z4tp", html);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, login.StatusCode);
        Assert.False(header.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Without_subscription_path_root_routes_stay_hidden()
    {
        // Старые установки без SubscriptionPath: панель по-прежнему только под секретным путём.
        using var plain = new PanelPathWebApplicationFactory(string.Empty);
        using var client = plain.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var rootLogin = await client.GetAsync("/login");
        var rootUsers = await client.GetAsync("/users");
        var login = await client.GetAsync("/k3x9q2m7w1v8z4tp/login");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, rootLogin.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, rootUsers.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
    }
}

public sealed class PanelPathWebApplicationFactory(string subscriptionPath = "/connect") : WebApplicationFactory<Program>
{
    private readonly string dataDirectory = Path.Combine(Path.GetTempPath(), $"frogplug-path-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("DataDirectory", dataDirectory);
        builder.UseSetting("PanelPath", "/k3x9q2m7w1v8z4tp");
        builder.UseSetting("SubscriptionPath", subscriptionPath);
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={Path.Combine(dataDirectory, "frogplug.db")}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, true);
        }
    }
}
