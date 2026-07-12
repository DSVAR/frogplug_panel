using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace FrogPlug.Agent;

public sealed class AgentOptions
{
    public string Listen { get; set; } = "127.0.0.1:9080";
    public string Token { get; set; } = string.Empty;
    public string PanelUrl { get; set; } = string.Empty;
    public bool AllowInsecurePanel { get; set; }
    public string XrayBinary { get; set; } = "/usr/local/bin/xray";
    public string XrayConfigPath { get; set; } = "/usr/local/etc/xray/config.json";
    public string GeoDir { get; set; } = "/usr/local/share/xray";
    public string CaddyfilePath { get; set; } = "/etc/caddy/Caddyfile";
    public string MtgBinary { get; set; } = "/usr/local/bin/mtg";
    public string ServiceMode { get; set; } = "systemd";
    public string PanelUpstream { get; set; } = string.Empty;
    public string PanelPath { get; set; } = string.Empty;
    public string SubscriptionPath { get; set; } = string.Empty;
    public string TgWebBinary { get; set; } = "/usr/local/bin/tproxy-server";
    public string HysteriaBinary { get; set; } = "/usr/local/bin/hysteria";
}

public static class KestrelConfiguration
{
    public static void ConfigureEndpoint(this KestrelServerOptions server, AgentOptions options)
    {
        var parts = options.Listen.Split(':', 2);
        var host = IPAddress.Loopback;
        var port = parts.Length == 2 && int.TryParse(parts[1], out var parsed) ? parsed : 9080;
        server.Listen(host, port);
    }
}
