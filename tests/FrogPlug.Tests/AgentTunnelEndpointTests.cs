using System.Net.WebSockets;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;

namespace FrogPlug.Tests;

public sealed class AgentTunnelEndpointTests : IAsyncLifetime
{
    private TunnelWebApplicationFactory factory = null!;

    public async Task InitializeAsync()
    {
        factory = new TunnelWebApplicationFactory();
        await factory.EnsureNodeAsync();
    }

    public Task DisposeAsync()
    {
        factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task TunnelWithWrongTokenReturnsUnauthorizedWithoutRegistration()
    {
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Authorization = "Bearer wrong";

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ConnectAsync(TunnelWebApplicationFactory.TunnelUri, CancellationToken.None));

        Assert.Contains("401", exception.Message);
        Assert.False(factory.Registry.IsConnected(TunnelWebApplicationFactory.NodeId));
    }

    [Fact]
    public async Task TunnelWithoutAuthorizationReturnsUnauthorized()
    {
        var client = factory.Server.CreateWebSocketClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ConnectAsync(TunnelWebApplicationFactory.TunnelUri, CancellationToken.None));

        Assert.Contains("401", exception.Message);
        Assert.False(factory.Registry.IsConnected(TunnelWebApplicationFactory.NodeId));
    }

    [Fact]
    public async Task TunnelWithCorrectTokenRegistersNode()
    {
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {TunnelWebApplicationFactory.Token}";
        using var socket = await client.ConnectAsync(TunnelWebApplicationFactory.TunnelUri, CancellationToken.None);

        await WaitUntilAsync(() => factory.Registry.IsConnected(TunnelWebApplicationFactory.NodeId));
        Assert.True(factory.Registry.IsConnected(TunnelWebApplicationFactory.NodeId));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}

public sealed class TunnelWebApplicationFactory : WebApplicationFactory<Program>
{
    public const int NodeId = 42;
    public const string Token = "tunnel-test-token";

    private readonly string dataDirectory = Path.Combine(Path.GetTempPath(), $"frogplug-tests-{Guid.NewGuid():N}");

    public static Uri TunnelUri { get; } = new("ws://localhost/agent/tunnel");

    public AgentTunnelRegistry Registry => Services.GetRequiredService<AgentTunnelRegistry>();

    public async Task EnsureNodeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Nodes.AnyAsync(item => item.Id == NodeId))
        {
            return;
        }

        db.Nodes.Add(new Node
        {
            Id = NodeId,
            Name = "Tunnel test node",
            Address = "127.0.0.1",
            Country = "Test",
            RealityPrivateKey = "private",
            RealityPublicKey = "public",
            RealityShortId = "12345678",
            AgentUrl = "http://127.0.0.1:9080",
            AgentToken = Token,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataDirectory"] = dataDirectory,
                ["ConnectionStrings:Default"] = $"Data Source={Path.Combine(dataDirectory, "frogplug.db")}" 
            });
        });
        builder.ConfigureLogging(logging => logging.ClearProviders());
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
