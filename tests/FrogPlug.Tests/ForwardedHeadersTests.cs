using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FrogPlug.Tests;

public sealed class ForwardedHeadersTests
{
    [Fact]
    public async Task LoopbackProxyUsesForwardedClientAddress()
    {
        var context = CreateContext(IPAddress.Loopback, "203.0.113.42");

        await InvokeMiddleware(context);

        Assert.Equal(IPAddress.Parse("203.0.113.42"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task NonProxyDoesNotUseForwardedClientAddress()
    {
        var context = CreateContext(IPAddress.Parse("198.51.100.10"), "203.0.113.42");

        await InvokeMiddleware(context);

        Assert.Equal(IPAddress.Parse("198.51.100.10"), context.Connection.RemoteIpAddress);
    }

    private static DefaultHttpContext CreateContext(IPAddress address, string forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = address;
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }

    private static Task InvokeMiddleware(HttpContext context)
    {
        var options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
            ForwardLimit = 1
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Add(IPAddress.Loopback);
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));
        return middleware.Invoke(context);
    }
}
