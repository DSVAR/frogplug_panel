using System.Net;
using FrogPlug.Agent.Services;
using FrogPlug.Core;

namespace FrogPlug.Agent.Endpoints;

public static class SniEndpoints
{
    public static void MapSniEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/mask/scan", async (SniScanRequest request, SniScanner scanner, CancellationToken token) =>
        {
            var baseAddress = LocalAddress();
            var result = await scanner.ScanAsync(baseAddress, request.Range, request.Limit, token);
            return Results.Ok(result);
        });
    }

    private static IPAddress LocalAddress()
    {
        return Dns.GetHostEntry(Dns.GetHostName()).AddressList
            .First(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));
    }
}
