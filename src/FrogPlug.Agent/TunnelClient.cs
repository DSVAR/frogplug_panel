using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using FrogPlug.Core;

namespace FrogPlug.Agent;

public sealed class TunnelClient(
    AgentOptions options,
    IHttpClientFactory clients,
    ILogger<TunnelClient> logger,
    TunnelConnectionFactory? connectionFactory = null) : BackgroundService
{
    private readonly SemaphoreSlim requests = new(4, 4);
    private readonly SemaphoreSlim sends = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(stoppingToken);
                delay = TimeSpan.FromSeconds(1);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Туннель агента отключён");
            }

            await Task.Delay(AddJitter(delay), stoppingToken);
            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 60));
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.PanelUrl) || string.IsNullOrWhiteSpace(options.Token))
        {
            throw new InvalidOperationException("Не заданы адрес панели или токен агента");
        }

        var uri = BuildTunnelUri(options.PanelUrl);
        using var socket = await ConnectAsync(uri, cancellationToken);
        await SendAsync(
            socket,
            TunnelSerializer.Serialize(new TunnelHello(
                Version,
                System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString())),
            cancellationToken);
        await ReceiveAsync(socket, cancellationToken);
    }

    private async Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (connectionFactory is not null)
        {
            return await connectionFactory(uri, options.Token, options.AllowInsecurePanel, cancellationToken);
        }

        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", $"Bearer {options.Token}");
        if (options.AllowInsecurePanel)
        {
            socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }

        await socket.ConnectAsync(uri, cancellationToken);
        return socket;
    }

    private async Task ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var text = await ReceiveTextAsync(socket, cancellationToken);
            if (text is null)
            {
                return;
            }

            var request = TunnelSerializer.Deserialize<TunnelRequest>(text);
            if (request is not null)
            {
                _ = HandleAsync(socket, request, cancellationToken);
            }
        }
    }

    private async Task HandleAsync(WebSocket socket, TunnelRequest request, CancellationToken cancellationToken)
    {
        await requests.WaitAsync(cancellationToken);
        try
        {
            var response = await ExecuteAsync(request, cancellationToken);
            await SendAsync(socket, TunnelSerializer.Serialize(response), cancellationToken);
        }
        finally
        {
            requests.Release();
        }
    }

    private async Task<TunnelResponse> ExecuteAsync(TunnelRequest request, CancellationToken cancellationToken)
    {
        if (string.Equals(request.Method, "PING", StringComparison.Ordinal))
        {
            return new TunnelResponse(request.Id, StatusCodes.Status204NoContent, null, null);
        }

        using var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Path);
        if (request.BodyJson is not null)
        {
            message.Content = new StringContent(request.BodyJson, Encoding.UTF8, "application/json");
        }

        var client = clients.CreateClient(nameof(TunnelClient));
        client.BaseAddress = new Uri($"http://{options.Listen}/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        using var response = await client.SendAsync(message, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new TunnelResponse(
            request.Id,
            (int)response.StatusCode,
            string.IsNullOrWhiteSpace(body) ? null : body,
            response.IsSuccessStatusCode ? null : body);
    }

    private static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

    private static Uri BuildTunnelUri(string panelUrl)
    {
        var panel = new Uri(panelUrl, UriKind.Absolute);
        var builder = new UriBuilder(panel)
        {
            Scheme = panel.Scheme == Uri.UriSchemeHttps ? "wss" : "ws",
            Path = panel.AbsolutePath.TrimEnd('/') + "/agent/tunnel",
            Query = string.Empty
        };
        return builder.Uri;
    }

    private static TimeSpan AddJitter(TimeSpan value)
    {
        var milliseconds = value.TotalMilliseconds * Random.Shared.NextDouble() * 0.2;
        return value + TimeSpan.FromMilliseconds(milliseconds);
    }

    private async Task SendAsync(WebSocket socket, string text, CancellationToken cancellationToken)
    {
        await sends.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(TunnelSerializer.ToUtf8(text), WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            sends.Release();
        }
    }

    private static async Task<string?> ReceiveTextAsync(WebSocket socket, CancellationToken cancellationToken)
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
