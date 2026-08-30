using FrogPlug.Server.Data;
using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task Unknown_token_returns_no_user()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var result = await service.Build("missing-token");

        Assert.Null(result.User);
        Assert.Equal(string.Empty, result.Body);
    }

    [Fact]
    public async Task Links_include_both_protocols_by_default()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await CreateEntryNode(fixture);
        var user = await fixture.Clients.Create(new ClientUserInput("alice", null));
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var links = await service.GetLinks(user);

        Assert.Equal(
            [SubscriptionLinkProtocol.Reality, SubscriptionLinkProtocol.Xhttp],
            links.Select(item => item.Protocol));
        Assert.All(links, item => Assert.StartsWith("vless://" + user.VlessUuid, item.Link));
        Assert.All(links, item => Assert.Equal("CZ", item.CountryCode));
    }

    [Fact]
    public async Task Links_skip_disallowed_protocols()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await CreateEntryNode(fixture);
        var input = new ClientUserInput("bob", null, AllowReality: false, AllowXhttp: true);
        var user = await fixture.Clients.Create(input);
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var links = await service.GetLinks(user);

        var link = Assert.Single(links);
        Assert.Equal(SubscriptionLinkProtocol.Xhttp, link.Protocol);
        Assert.Contains("type=xhttp", link.Link);
    }

    [Fact]
    public async Task Build_returns_base64_with_only_allowed_links()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await CreateEntryNode(fixture);
        var input = new ClientUserInput("carol", null, AllowReality: true, AllowXhttp: false);
        var user = await fixture.Clients.Create(input);
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var result = await service.Build(user.SubscriptionToken);

        var body = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(result.Body));
        var line = Assert.Single(body.Split('\n'));
        Assert.Contains("security=reality", line);
    }

    [Fact]
    public async Task Links_skip_protocols_disabled_on_node()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await CreateEntryNode(fixture);
        node.RealityEnabled = false;
        node.XhttpEnabled = false;
        await fixture.Db.SaveChangesAsync();
        var user = await fixture.Clients.Create(new ClientUserInput("dora", null));
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var links = await service.GetLinks(user);

        Assert.Empty(links);
    }

    [Fact]
    public async Task Whitelist_links_are_ordered_and_limited_to_whitelist_users()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await CreateEntryNode(fixture);
        var exit = await fixture.Nodes.Create(new NodeInput("exit", "198.51.100.11", "DE", NodeRole.Exit));
        await fixture.Nodes.Create(new NodeInput("ru-1", "198.51.100.12", "RU", NodeRole.Entry | NodeRole.WhitelistEntry,
            MaskMode: FrogPlug.Core.MaskMode.ForeignSite, RealitySni: "cdn.mos.ru", WhitelistExitNodeId: exit.Id));
        var whitelistOnly = await fixture.Clients.Create(new ClientUserInput("white", null, AllowReality: false, AllowXhttp: false, AllowWhitelist: true));
        var both = await fixture.Clients.Create(new ClientUserInput("both", null, AllowWhitelist: true));
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var onlyLinks = await service.GetLinks(whitelistOnly);
        var links = await service.GetLinks(both);

        var only = Assert.Single(onlyLinks);
        Assert.Contains(Uri.EscapeDataString("🇷🇺 white · ru-1 · Reality"), only.Link);
        Assert.Contains(Uri.EscapeDataString("🇨🇿 both · cz-1 · Reality"), links[0].Link);
        Assert.Contains(Uri.EscapeDataString("🇷🇺 both · ru-1 · XHTTP"), links.Last().Link);
    }

    [Fact]
    public async Task Openflux_subscription_filters_profiles_by_transport()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput("flux", "198.51.100.10", "CZ", NodeRole.Exit));
        fixture.Db.OpenFluxChannels.Add(new OpenFluxChannel
        {
            NodeId = node.Id,
            Name = "telemost",
            Key = new string('a', 64),
            Port = 8445,
            TelemostUrl = "https://telemost.yandex.ru/j/123456"
        });
        fixture.Db.OpenFluxChannels.Add(new OpenFluxChannel
        {
            NodeId = node.Id,
            Name = "mailru",
            Key = new string('b', 64),
            Port = 8446,
            MailruUrl = "https://cloud.mail.ru/public/ab/cdef"
        });
        await fixture.Db.SaveChangesAsync();
        var user = await fixture.Clients.Create(new ClientUserInput("white", null, AllowWhitelist: true));
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var result = await service.BuildOpenFlux(user.SubscriptionToken, new HashSet<string> { "telemost" });

        Assert.Contains("\"status\":\"active\"", result.Body);
        Assert.Contains("\"id\":\"channel-1\"", result.Body);
        Assert.DoesNotContain("mailru", result.Body);
    }

    [Fact]
    public async Task Openflux_subscription_returns_expired_status_without_profiles()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var user = await fixture.Clients.Create(new ClientUserInput(
            "expired",
            null,
            ExpiresAt: fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1),
            AllowWhitelist: true));
        var service = new SubscriptionService(fixture.Db, fixture.Settings);

        var result = await service.BuildOpenFlux(user.SubscriptionToken, new HashSet<string> { "telemost" });

        Assert.Contains("\"status\":\"expired\"", result.Body);
        Assert.Contains("\"profiles\":[]", result.Body);
    }
    private static async Task<Node> CreateEntryNode(ServiceFixture fixture)
    {
        var input = new NodeInput("cz-1", "198.51.100.10", "CZ", NodeRole.Entry, Domain: "cz.example.com");
        return await fixture.Nodes.Create(input);
    }
}
