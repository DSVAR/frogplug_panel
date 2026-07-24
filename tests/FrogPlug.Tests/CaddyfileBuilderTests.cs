using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class CaddyfileBuilderTests
{
    [Fact]
    public void Xhttp_is_proxied_in_its_own_handle_before_the_stub_site()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123");

        Assert.Contains("https://vpn.example:8443 {", config);
        Assert.Contains("    bind 127.0.0.1", config);
        Assert.Contains("    handle /abc123* {\n        reverse_proxy h2c://127.0.0.1:10443\n    }", config);
        Assert.True(config.IndexOf("handle /abc123*", StringComparison.Ordinal) <
            config.IndexOf("try_files", StringComparison.Ordinal));
    }

    [Fact]
    public void Stub_site_answers_unknown_paths_with_real_404_page()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123");

        Assert.Contains("        try_files {path} {path}.html {path}/index.html", config);
        Assert.DoesNotContain("/index.html\n", config.Replace("{path}/index.html", string.Empty));
        Assert.Contains("    handle_errors {\n        root * /var/lib/frogplug/site\n        rewrite * /404.html", config);
    }

    [Fact]
    public void Plain_http_redirects_to_https()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123");

        Assert.Contains("http://vpn.example {\n    redir https://{host}{uri} permanent\n}", config);
    }

    [Fact]
    public void Uses_the_domain_as_the_default_sni()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123");

        // default_sni — глобальная опция Caddy, внутри servers он конфиг не примет.
        Assert.Contains("{\n    default_sni vpn.example\n    servers {", config);
    }

    [Fact]
    public void Plain_http_for_an_unknown_host_returns_404()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123");

        Assert.Contains("http:// {\n    @foreign not host vpn.example\n    respond @foreign 404\n}", config);
    }

    [Fact]
    public void Does_not_serve_the_not_found_document_directly()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123");

        Assert.Contains("    @direct404 path /404 /404.html\n    respond @direct404 404", config);
    }

    [Fact]
    public void Adds_slash_to_relative_path()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "abc123");

        Assert.Contains("    handle /abc123* {", config);
    }

    [Fact]
    public void Rejects_missing_domain()
    {
        Assert.Throws<ArgumentException>(() => CaddyfileBuilder.Build(" ", "/abc123"));
    }

    [Fact]
    public void Rejects_missing_path()
    {
        Assert.Throws<ArgumentException>(() => CaddyfileBuilder.Build("vpn.example", " "));
    }

    [Fact]
    public void Disabled_xhttp_has_no_reverse_proxy()
    {
        var config = CaddyfileBuilder.Build("vpn.example", string.Empty, xhttpEnabled: false);

        Assert.DoesNotContain("reverse_proxy", config);
        Assert.Contains("file_server", config);
    }

    [Fact]
    public void Panel_is_proxied_under_its_secret_path_without_stripping_it()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123", panelUpstream: "127.0.0.1:5280", panelPath: "/s3cr3tpath");

        Assert.Contains("    handle /s3cr3tpath* {\n        reverse_proxy 127.0.0.1:5280\n    }", config);
        Assert.DoesNotContain("handle_path", config);
    }

    [Fact]
    public void Subscription_path_is_proxied_to_the_panel()
    {
        var config = CaddyfileBuilder.Build(
            "vpn.example",
            "/abc123",
            panelUpstream: "127.0.0.1:5280",
            panelPath: "/admin",
            subscriptionPath: "/connect");

        Assert.Contains("    handle /admin* {\n        reverse_proxy 127.0.0.1:5280\n    }", config);
        Assert.Contains("    handle /connect* {\n        reverse_proxy 127.0.0.1:5280\n    }", config);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/")]
    public void Panel_on_site_root_is_rejected(string? panelPath)
    {
        Assert.Throws<ArgumentException>(() =>
            CaddyfileBuilder.Build("vpn.example", "/abc123", panelUpstream: "127.0.0.1:5280", panelPath: panelPath));
    }

    [Fact]
    public void Mtproto_host_gets_its_own_stub_site_block()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123", mtprotoHost: "tg.vpn.example");

        Assert.Contains("https://tg.vpn.example:8443 {", config);
        Assert.Equal(4, config.Split("file_server").Length - 1);
        Assert.EndsWith("}\n", config);
    }

    [Fact]
    public void Telegram_web_proxies_the_whole_own_domain_before_the_stub_site()
    {
        var config = CaddyfileBuilder.Build("vpn.example", "/abc123", tgWebEnabled: true);

        Assert.Contains("    handle {\n        reverse_proxy 127.0.0.1:8080\n    }", config);
        Assert.True(config.IndexOf("reverse_proxy 127.0.0.1:8080", StringComparison.Ordinal) <
            config.IndexOf("try_files", StringComparison.Ordinal));
    }
}
