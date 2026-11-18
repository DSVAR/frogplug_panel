namespace FrogPlug.Core;

public static class CaddyfileBuilder
{
    private const string SiteRoot = "/var/lib/frogplug/site";

    // Маршруты прокси обрабатываются раньше сайта-заглушки.
    public static string Build(
        string domain,
        string xhttpPath,
        int upstreamPort = NodePorts.Xhttp,
        bool xhttpEnabled = true,
        string? mtprotoHost = null,
        string? panelUpstream = null,
        string? panelPath = null,
        string? subscriptionPath = null,
        bool tgWebEnabled = false,
        string? docsHost = null)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            throw new ArgumentException("Домен обязателен", nameof(domain));
        }

        if (xhttpEnabled && string.IsNullOrWhiteSpace(xhttpPath))
        {
            throw new ArgumentException("Путь XHTTP обязателен", nameof(xhttpPath));
        }

        var host = domain.Trim();
        var docs = docsHost?.Trim();
        var lines = new List<string>
        {
            "{",
            // Без SNI (сканеры по IP) Caddy отдаёт настоящий сертификат домена, а не TLS-ошибку.
            $"    default_sni {host}",
            "    servers {",
            "        listener_wrappers {",
            "            proxy_protocol {",
            "                allow 127.0.0.1/32",
            "            }",
            "            tls",
            "        }",
            // Caddy слушает только loopback за HAProxy: HTTP/3 недоступен, а Alt-Svc выдал бы внутренний порт.
            "        protocols h1 h2",
            "    }",
            "}",
            string.Empty,
            $"http://{host} {{",
            "    redir https://{host}{uri} permanent",
            "}",
            string.Empty,
            "http:// {",
            $"    @foreign not host {host}{(string.IsNullOrWhiteSpace(docs) ? string.Empty : $" {docs}")}",
            "    respond @foreign 404",
            "}",
            string.Empty,
            $"https://{host}:{NodePorts.CaddyTls} {{",
            "    bind 127.0.0.1"
        };
        if (!string.IsNullOrWhiteSpace(panelUpstream))
        {
            // Путь панели.
            AddHandle(lines, NormalizePrefix(panelPath, nameof(panelPath)), $"reverse_proxy {panelUpstream.Trim()}");
            if (!string.IsNullOrWhiteSpace(subscriptionPath))
            {
                // Путь подписок.
                AddHandle(lines, NormalizePrefix(subscriptionPath, nameof(subscriptionPath)), $"reverse_proxy {panelUpstream.Trim()}");
            }
        }

        if (xhttpEnabled)
        {
            // XHTTP через Caddy.
            AddHandle(lines, NormalizePrefix(xhttpPath, nameof(xhttpPath)), $"reverse_proxy h2c://127.0.0.1:{upstreamPort}");
        }

        if (tgWebEnabled)
        {
            lines.Add("    handle {");
            lines.Add("        reverse_proxy 127.0.0.1:8080");
            lines.Add("    }");
        }

        AddStubSite(lines);
        lines.Add("}");
        if (!string.IsNullOrWhiteSpace(docs) &&
            !string.Equals(docs, host, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(docs, mtprotoHost?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(string.Empty);
            lines.Add($"http://{docs} {{");
            lines.Add("    redir https://{host}{uri} permanent");
            lines.Add("}");
            lines.Add(string.Empty);
            lines.Add($"https://{docs}:{NodePorts.CaddyTls} {{");
            lines.Add("    bind 127.0.0.1");
            AddStubSite(lines);
            lines.Add("}");
        }
        if (!string.IsNullOrWhiteSpace(mtprotoHost) &&
            !string.Equals(mtprotoHost.Trim(), host, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(string.Empty);
            lines.Add($"https://{mtprotoHost.Trim()}:{NodePorts.CaddyTls} {{");
            lines.Add("    bind 127.0.0.1");
            AddStubSite(lines);
            lines.Add("}");
        }

        return string.Join('\n', lines) + '\n';
    }

    private static string NormalizePrefix(string? path, string name)
    {
        var value = path?.Trim() ?? string.Empty;
        if (value.Length == 0 || value == "/")
        {
            throw new ArgumentException("Путь должен быть непустым и отличаться от корня сайта", name);
        }

        return value.StartsWith('/') ? value.TrimEnd('/') : "/" + value.TrimEnd('/');
    }

    private static void AddHandle(List<string> lines, string prefix, string directive)
    {
        lines.Add($"    handle {prefix}* {{");
        lines.Add($"        {directive}");
        lines.Add("    }");
    }

    // Обычный сайт: настоящие файлы, для остальных адресов — честный 404 со своей страницей.
    private static void AddStubSite(List<string> lines)
    {
        lines.Add("    @direct404 path /404 /404.html");
        lines.Add("    respond @direct404 404");
        lines.Add("    handle {");
        lines.Add($"        root * {SiteRoot}");
        lines.Add("        try_files {path} {path}.html {path}/index.html");
        lines.Add("        file_server");
        lines.Add("    }");
        lines.Add("    handle_errors {");
        lines.Add($"        root * {SiteRoot}");
        lines.Add("        rewrite * /404.html");
        lines.Add("        file_server");
        lines.Add("    }");
    }
}
