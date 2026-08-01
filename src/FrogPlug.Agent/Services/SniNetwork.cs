using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FrogPlug.Core;

namespace FrogPlug.Agent.Services;

public sealed class SniNetwork : ISniNetwork
{
    public async Task<SniProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken token)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeoutSource.CancelAfter(timeout);
        try
        {
            var host = await ReadHostAsync(address, timeoutSource.Token);
            if (host is null)
            {
                return SniProbeResult.Rejected("hostname");
            }

            return await ProbeHostAsync(address, host, timeoutSource.Token);
        }
        catch (OperationCanceledException)
        {
            return SniProbeResult.Rejected("timeout");
        }
        catch (Exception)
        {
            return SniProbeResult.Rejected("connection");
        }
    }

    private static async Task<string?> ReadHostAsync(IPAddress address, CancellationToken token)
    {
        X509Certificate? captured = null;
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(address, 443, token);
        using var ssl = new SslStream(tcp.GetStream(), false, (_, certificate, _, _) =>
        {
            captured = certificate;
            return true;
        });
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = address.ToString(),
            EnabledSslProtocols = SslProtocols.Tls13,
            ApplicationProtocols = [SslApplicationProtocol.Http2]
        }, token);
        return await SelectHostAsync(captured, token);
    }

    private static async Task<string?> SelectHostAsync(X509Certificate? certificate, CancellationToken token)
    {
        if (certificate is null)
        {
            return null;
        }

        var parsed = new X509Certificate2(certificate);
        var name = parsed.GetNameInfo(X509NameType.DnsName, false)?.TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (!name.StartsWith("*.", StringComparison.Ordinal))
        {
            return SniCandidateRules.IsRealDomain(name) ? name : null;
        }

        var concreteName = name[2..];
        if (!SniCandidateRules.IsRealDomain(concreteName))
        {
            return null;
        }

        var addresses = await Dns.GetHostAddressesAsync(concreteName, token);
        return addresses.Length > 0 ? concreteName : null;
    }

    private static async Task<SniProbeResult> ProbeHostAsync(IPAddress address, string host, CancellationToken token)
    {
        SslPolicyErrors errors = SslPolicyErrors.None;
        X509Certificate? captured = null;
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(address, 443, token);
        using var ssl = new SslStream(tcp.GetStream(), false, (_, certificate, chain, policyErrors) =>
        {
            captured = certificate;
            errors = policyErrors;
            return true;
        });
        var stopwatch = Stopwatch.StartNew();
        await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
        {
            TargetHost = host,
            EnabledSslProtocols = SslProtocols.Tls13,
            ApplicationProtocols = [SslApplicationProtocol.Http2]
        }, token);
        stopwatch.Stop();
        if (captured is null)
        {
            return SniProbeResult.Rejected("certificate");
        }

        var certificate = new X509Certificate2(captured);
        var redirectHost = await ReadRedirectAsync(address, host, token);
        var candidate = new SniCandidateInput(host, errors == SslPolicyErrors.None,
            certificate.NotAfter <= DateTime.UtcNow, certificate.Subject == certificate.Issuer,
            ssl.SslProtocol, Encoding.ASCII.GetString(ssl.NegotiatedApplicationProtocol.Protocol.Span), certificate.Issuer,
            [host], redirectHost, stopwatch.ElapsedMilliseconds);
        return new SniProbeResult(candidate, null);
    }

    private static async Task<string?> ReadRedirectAsync(IPAddress address, string host, CancellationToken token)
    {
        using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        handler.ConnectCallback = async (_, cancellationToken) =>
        {
            var tcp = new TcpClient();
            await tcp.ConnectAsync(address, 443, cancellationToken);
            var ssl = new SslStream(tcp.GetStream(), false);
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls13,
                ApplicationProtocols = [SslApplicationProtocol.Http2]
            }, cancellationToken);
            return ssl;
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var response = await client.GetAsync("https://" + host + "/", token);
        if ((int)response.StatusCode is < 300 or >= 400 || response.Headers.Location is null)
        {
            return null;
        }

        return response.Headers.Location.Host;
    }
}
