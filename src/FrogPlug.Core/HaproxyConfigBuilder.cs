namespace FrogPlug.Core;

public static class HaproxyConfigBuilder
{
    // Маршрутизация 443 по SNI.
    public static string Build(bool mtprotoEnabled, string? mtprotoHost)
    {
        var routeMtproto = mtprotoEnabled && !string.IsNullOrWhiteSpace(mtprotoHost);
        var lines = new List<string>
        {
            "global",
            "    daemon",
            string.Empty,
            "defaults",
            "    mode tcp",
            "    timeout connect 5s",
            "    timeout client 1h",
            "    timeout server 1h",
            "    timeout tunnel 1h",
            "    timeout client-fin 30s",
            string.Empty,
            "frontend frogplug-https",
            "    bind :443",
            "    tcp-request inspect-delay 5s",
            "    tcp-request content accept if { req_ssl_hello_type 1 }"
        };
        if (routeMtproto)
        {
            lines.Add($"    use_backend frogplug-mtproto if {{ req.ssl_sni -i {mtprotoHost!.Trim()} }}");
        }

        lines.Add("    default_backend frogplug-reality");
        lines.Add(string.Empty);
        lines.Add("backend frogplug-reality");
        lines.Add("    mode tcp");
        lines.Add($"    server reality 127.0.0.1:{NodePorts.Reality} send-proxy-v2");
        if (routeMtproto)
        {
            lines.Add(string.Empty);
            lines.Add("backend frogplug-mtproto");
            lines.Add("    mode tcp");
            lines.Add($"    server mtproto 127.0.0.1:{NodePorts.Mtproto} send-proxy-v2");
        }

        return string.Join('\n', lines) + '\n';
    }
}
