extern alias AgentAssembly;

using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using AgentOptions = AgentAssembly::FrogPlug.Agent.AgentOptions;
using AgentTunnelClient = AgentAssembly::FrogPlug.Agent.TunnelClient;
using AgentTunnelConnectionFactory = AgentAssembly::FrogPlug.Agent.TunnelConnectionFactory;

namespace FrogPlug.Tests;

public sealed class TunnelEndToEndTests
{
    [Fact]
    public async Task AgentClientGetStatusReturnsStatusFromTunnelAgent()
    {
        using var factory = new TunnelWebApplicationFactory();
        await factory.EnsureNodeAsync();
        var status = CreateStatus();
        var handler = new StatusHandler(status);
        var clients = new StatusHttpClientFactory(handler);
        var options = new AgentOptions
        {
            PanelUrl = "http://localhost",
            Token = TunnelWebApplicationFactory.Token
        };
        using var tunnel = new AgentTunnelClient(
            options,
            clients,
            NullLogger<AgentTunnelClient>.Instance,
            ConnectToTestServerAsync(factory));

        await tunnel.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => factory.Registry.IsConnected(TunnelWebApplicationFactory.NodeId));
        var node = new Node
        {
            Id = TunnelWebApplicationFactory.NodeId,
            Name = "node",
            Address = "127.0.0.1",
            Country = "Test",
            RealityPrivateKey = "private",
            RealityPublicKey = "public",
            RealityShortId = "12345678",
            AgentUrl = string.Empty,
            AgentToken = TunnelWebApplicationFactory.Token
        };
        var actual = await new AgentClient(factory.Registry).GetStatus(node);

        Assert.NotNull(actual);
        Assert.Equal(status.Version, actual.Version);
        Assert.Equal(status.Hostname, actual.Hostname);
        Assert.Equal(status.UptimeSeconds, actual.UptimeSeconds);
        await tunnel.StopAsync(CancellationToken.None);
    }

    private static AgentTunnelConnectionFactory ConnectToTestServerAsync(TunnelWebApplicationFactory factory)
    {
        return async (uri, token, _, cancellationToken) =>
        {
            var client = factory.Server.CreateWebSocketClient();
            client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {token}";
            return await client.ConnectAsync(uri, cancellationToken);
        };
    }

    private static AgentStatus CreateStatus()
    {
        return new AgentStatus(
            "1.2.3",
            "test-host",
            123,
            0.5,
            456,
            789,
            new ComponentStatus(true, true, "1.2.3"),
            new ComponentStatus(true, false),
            new ComponentStatus(false, false),
            new OpenFluxStatus(false, []));
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

public sealed class StatusHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name)
    {
        return new HttpClient(handler, false);
    }
}

public sealed class StatusHandler(AgentStatus status) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("/status", request.RequestUri!.AbsolutePath);
        var content = new StringContent(TunnelSerializer.Serialize(status));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }
}
