using System.Security.Cryptography;
using System.Text;

namespace FrogPlug.Agent;

public sealed class BearerAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AgentOptions _options;

    public BearerAuthMiddleware(RequestDelegate next, AgentOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }
        if (!IsAuthorized(context.Request.Headers.Authorization.ToString(), _options.Token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        await _next(context);
    }

    public static bool IsAuthorized(string header, string token)
    {
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var supplied = Encoding.UTF8.GetBytes(header[prefix.Length..]);
        var expected = Encoding.UTF8.GetBytes(token ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }
}
