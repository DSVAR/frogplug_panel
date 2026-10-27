using System.Collections;
using System.Net.WebSockets;
using System.Reflection;
using FrogPlug.Core;
using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class AgentTunnelRegistryTests
{
    [Fact]
    public async Task SendAsyncCorrelatesResponsesByRequestId()
    {
        var registry = new AgentTunnelRegistry();
        await using var pair = new InMemoryWebSocketPair();
        using var cancellation = new CancellationTokenSource();
        var registration = registry.RegisterAsync(1, pair.Panel, cancellation.Token);
        var first = registry.SendAsync<string>(1, "GET", "/first", null, null, CancellationToken.None);
        var second = registry.SendAsync<string>(1, "GET", "/second", null, null, CancellationToken.None);
        var firstRequest = await ReceiveRequestAsync(pair.Agent);
        var secondRequest = await ReceiveRequestAsync(pair.Agent);
        await SendResponseAsync(pair.Agent, secondRequest.Id, "second");
        await SendResponseAsync(pair.Agent, firstRequest.Id, "first");

        Assert.Equal("first", await first);
        Assert.Equal("second", await second);
        await StopRegistrationAsync(cancellation, registration);
    }

    [Fact]
    public async Task SendAsyncTimeoutThrowsAndRemovesPendingRequest()
    {
        var registry = new AgentTunnelRegistry();
        await using var pair = new InMemoryWebSocketPair();
        using var cancellation = new CancellationTokenSource();
        var registration = registry.RegisterAsync(1, pair.Panel, cancellation.Token);
        var sending = registry.SendAsync<string>(1, "GET", "/slow", null, TimeSpan.FromMilliseconds(20), CancellationToken.None);
        await ReceiveRequestAsync(pair.Agent);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await sending);
        Assert.Equal(0, GetPendingCount(registry));
        await StopRegistrationAsync(cancellation, registration);
    }

    [Fact]
    public async Task RegisterAsyncReplacesConnectionAndFailsItsPendingRequests()
    {
        var registry = new AgentTunnelRegistry();
        await using var firstPair = new InMemoryWebSocketPair();
        await using var secondPair = new InMemoryWebSocketPair();
        using var cancellation = new CancellationTokenSource();
        var firstRegistration = registry.RegisterAsync(1, firstPair.Panel, cancellation.Token);
        var pending = registry.SendAsync<string>(1, "GET", "/slow", null, null, CancellationToken.None);
        await ReceiveRequestAsync(firstPair.Agent);
        var closeReply = ReplyToCloseAsync(firstPair.Agent);
        var secondRegistration = registry.RegisterAsync(1, secondPair.Panel, cancellation.Token);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await pending);
        Assert.Contains("Соединение с нодой закрыто", exception.Message);
        await closeReply;
        await firstRegistration;
        await StopRegistrationAsync(cancellation, secondRegistration);
    }

    [Fact]
    public async Task SendAsyncForUnknownNodeThrowsExpectedMessage()
    {
        var registry = new AgentTunnelRegistry();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await registry.SendAsync<string>(1, "GET", "/status", null, null, CancellationToken.None));

        Assert.Equal("Нода не подключена к панели", exception.Message);
    }

    private static async Task<TunnelRequest> ReceiveRequestAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
        var text = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
        return TunnelSerializer.Deserialize<TunnelRequest>(text)!;
    }

    private static Task SendResponseAsync(WebSocket socket, string id, string body)
    {
        var response = TunnelSerializer.Serialize(new TunnelResponse(id, 200, TunnelSerializer.Serialize(body), null));
        return socket.SendAsync(TunnelSerializer.ToUtf8(response), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task ReplyToCloseAsync(WebSocket socket)
    {
        var buffer = new byte[4096];
        await socket.ReceiveAsync(buffer, CancellationToken.None);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closed", CancellationToken.None);
    }

    private static int GetPendingCount(AgentTunnelRegistry registry)
    {
        var connections = registry.GetType().GetField("connections", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var values = ((IDictionary)connections.GetValue(registry)!).Values;
        var connection = values.Cast<object>().Single();
        var pending = connection.GetType().GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((IDictionary)pending.GetValue(connection)!).Count;
    }

    private static async Task StopRegistrationAsync(CancellationTokenSource cancellation, Task registration)
    {
        cancellation.Cancel();
        try
        {
            await registration;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
