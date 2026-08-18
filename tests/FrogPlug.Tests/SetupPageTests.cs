using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FrogPlug.Tests;

public sealed class SetupPageTests : IAsyncLifetime
{
    private SetupWebApplicationFactory factory = null!;

    public Task InitializeAsync()
    {
        factory = new SetupWebApplicationFactory();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Setup_without_token_does_not_render_wizard_form()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/setup");
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Откройте ссылку из консоли установки", content);
        Assert.DoesNotContain("vpn.example.com", content);
        Assert.DoesNotContain("name=\"setup\"", content);
    }
}

public sealed class SetupWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string dataDirectory = Path.Combine(Path.GetTempPath(), $"frogplug-setup-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("DataDirectory", dataDirectory);
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
