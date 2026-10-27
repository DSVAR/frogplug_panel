using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using FrogPlug.Core;

namespace FrogPlug.Server.Services;

public sealed class AgentTunnelRegistry
{
    private readonly ConcurrentDictionary<int, AgentTunnelConnection> connections = new();

    public bool IsConnected(int nodeId)
    {
        return connections.TryGetValue(nodeId, out var connection) && connection.IsAlive;
    }

    public DateTime? LastConnectedAt(int nodeId)
    {
        return connections.TryGetValue(nodeId, out var connection)
            ? connection.ConnectedAt
            : null;
    }

    public async Task RegisterAsync(int nodeId, WebSocket socket, CancellationToken cancellationToken)
    {
        var connection = new AgentTunnelConnection(socket);
        if (connections.TryGetValue(nodeId, out var previous))
        {
            await previous.CloseAsync();
        }

        connections[nodeId] = connection;
        try
        {
            await connection.RunAsync(cancellationToken);
        }
        finally
        {
            connections.TryRemove(new KeyValuePair<int, AgentTunnelConnection>(nodeId, connection));
            connection.FailPending();
            await connection.CloseAsync();
        }
    }

    public async Task<T?> SendAsync<T>(
        int nodeId,
        string method,
        string path,
        object? body,
        TimeSpan? timeout,
        CancellationToken cancellationToken)
    {
        if (!connections.TryGetValue(nodeId, out var connection) || !connection.IsAlive)
        {
            throw new InvalidOperationException("Нода не подключена к панели");
        }

        var bodyJson = body is null ? null : TunnelSerializer.Serialize(body);
        var response = await connection.SendAsync(
            new TunnelRequest(Guid.NewGuid().ToString("N"), method, path, bodyJson),
            timeout ?? TimeSpan.FromSeconds(30),
            cancellationToken);
        if (response.Status is < 200 or >= 300)
        {
            throw new HttpRequestException($"Агент вернул {response.Status}: {response.Error}");
        }

        if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(response.BodyJson))
        {
            return default;
        }

        return TunnelSerializer.Deserialize<T>(response.BodyJson);
    }

    private sealed class AgentTunnelConnection(WebSocket socket)
    {
        private readonly SemaphoreSlim sendLock = new(1, 1);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<TunnelResponse>> pending = new();

        public DateTime ConnectedAt { get; } = DateTime.UtcNow;

        public DateTime LastFrameAt { get; private set; } = DateTime.UtcNow;

        public bool IsAlive => socket.State == WebSocketState.Open;

        public async Task<TunnelResponse> SendAsync(
            TunnelRequest request,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<TunnelResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!pending.TryAdd(request.Id, completion))
            {
                throw new InvalidOperationException("Не удалось сопоставить запрос туннеля");
            }

            try
            {
                await SendTextAsync(TunnelSerializer.Serialize(request), cancellationToken);
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(timeout);
                return await completion.Task.WaitAsync(timeoutSource.Token);
            }
            finally
            {
                pending.TryRemove(request.Id, out _);
            }
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var receive = ReceiveAsync(cancellationToken);
            while (!receive.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await Task.WhenAny(receive, Task.Delay(TimeSpan.FromSeconds(20), cancellationToken));
                if (receive.IsCompleted)
                {
                    break;
                }

                if (DateTime.UtcNow - LastFrameAt > TimeSpan.FromSeconds(60))
                {
                    return;
                }

                await SendTextAsync(
                    TunnelSerializer.Serialize(new TunnelRequest(
                        Guid.NewGuid().ToString("N"),
                        "PING",
                        "/",
                        null)),
                    cancellationToken);
            }

            await receive;
        }

        private async Task ReceiveAsync(CancellationToken cancellationToken)
        {
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var text = await ReceiveTextAsync(cancellationToken);
                if (text is null)
                {
                    return;
                }

                var response = TunnelSerializer.Deserialize<TunnelResponse>(text);
                if (response is not null && response.Id is not null && pending.TryGetValue(response.Id, out var completion))
                {
                    completion.TrySetResult(response);
                }

                LastFrameAt = DateTime.UtcNow;
            }
        }

        public void FailPending()
        {
            foreach (var completion in pending.Values)
            {
                completion.TrySetException(new InvalidOperationException("Соединение с нодой закрыто"));
            }
        }

        public async Task CloseAsync()
        {
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Заменено", CancellationToken.None);
            }
        }

        private async Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            var bytes = TunnelSerializer.ToUtf8(text);
            await sendLock.WaitAsync(cancellationToken);
            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
            }
            finally
            {
                sendLock.Release();
            }
        }

        private async Task<string?> ReceiveTextAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[16 * 1024];
            using var stream = new MemoryStream();
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    throw new InvalidOperationException("Ожидался текстовый кадр туннеля");
                }

                stream.Write(buffer, 0, result.Count);
                if (stream.Length > TunnelSerializer.MaxMessageBytes)
                {
                    throw new InvalidOperationException("Сообщение туннеля превышает 8 МБ");
                }

                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
                }
            }
        }
    }
}
