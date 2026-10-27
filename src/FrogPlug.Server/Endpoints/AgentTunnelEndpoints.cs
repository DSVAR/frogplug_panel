using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Endpoints;

public static class AgentTunnelEndpoints
{
    public static IEndpointRouteBuilder MapAgentTunnelEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/agent/tunnel", HandleAsync)
            .AllowAnonymous()
            .RequireRateLimiting("public");
        return endpoints;
    }

    private static async Task HandleAsync(
        HttpContext context,
        AppDbContext db,
        AgentTunnelRegistry registry,
        SyncQueue syncQueue,
        CancellationToken cancellationToken)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var token = ReadBearerToken(context.Request.Headers.Authorization);
        var nodes = await db.Nodes.AsNoTracking().ToListAsync(cancellationToken);
        var node = nodes.FirstOrDefault(item => TokensEqual(item.AgentToken, token));
        if (node is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        // Нода могла перезапуститься с пустым состоянием служб: применяем конфиг заново (с паузой очереди, туннель успеет зарегистрироваться).
        syncQueue.RequestAll();
        await registry.RegisterAsync(node.Id, socket, cancellationToken);
    }

    private static string? ReadBearerToken(string? authorization)
    {
        const string prefix = "Bearer ";
        return authorization is not null && authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[prefix.Length..]
            : null;
    }

    private static bool TokensEqual(string stored, string? received)
    {
        if (received is null)
        {
            return false;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(stored));
        var receivedHash = SHA256.HashData(Encoding.UTF8.GetBytes(received));
        return CryptographicOperations.FixedTimeEquals(expectedHash, receivedHash);
    }
}
