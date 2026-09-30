using FrogPlug.Core;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;
using System.Text.Json;

namespace FrogPlug.Tests;

public sealed class TelegramNotifierTests
{
    [Fact]
    public async Task Unconfigured_notifier_does_not_call_http()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var factory = new CountingHttpClientFactory();
        var notifier = new TelegramNotifier(factory, fixture.Settings, NullLogger<TelegramNotifier>.Instance);
        Assert.False(await notifier.Send("test"));
        Assert.Equal(0, factory.Calls);
    }

    [Theory]
    [InlineData("<one>&\"two\"", "&lt;one&gt;&amp;&quot;two&quot;")]
    [InlineData("обычный текст", "обычный текст")]
    [InlineData("<", "&lt;")]
    [InlineData(">", "&gt;")]
    [InlineData("&", "&amp;")]
    [InlineData("\"", "&quot;")]
    [InlineData("<&>", "&lt;&amp;&gt;")]
    [InlineData("a < b", "a &lt; b")]
    [InlineData("a > b", "a &gt; b")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("<b>текст</b>", "&lt;b&gt;текст&lt;/b&gt;")]
    [InlineData("строка\n<тег>", "строка\n&lt;тег&gt;")]
    [InlineData("'кавычки'", "'кавычки'")]
    public void Escapes_html(string value, string expected)
    {
        Assert.Equal(expected, TelegramNotifier.Escape(value));
    }

    [Fact]
    public async Task Configured_notifier_posts_escaped_html_message()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        await fixture.Settings.SetTelegramBotToken("secret-token");
        await fixture.Settings.SetTelegramChatId("123");
        var handler = new RecordingHandler();
        var notifier = new TelegramNotifier(
            new HandlerHttpClientFactory(handler),
            fixture.Settings,
            NullLogger<TelegramNotifier>.Instance);
        Assert.True(await notifier.Send("<test> & message"));
        Assert.Equal("https://api.telegram.org/botsecret-token/sendMessage", handler.RequestUri);
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("&lt;test&gt; &amp; message", document.RootElement.GetProperty("text").GetString());
        Assert.Equal("HTML", document.RootElement.GetProperty("parseMode").GetString());
    }

    [Fact]
    public async Task Offline_notification_is_sent_once_on_third_failed_poll_and_once_on_recovery()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = AddNode(fixture);
        await fixture.Db.SaveChangesAsync();
        var agent = new FakeAgentClient();
        agent.ThrowOnStatus.Add(node.Name);
        var notifier = new RecordingNotifier();
        var monitor = new NodeMonitor(fixture.Db, agent, fixture.Clock, notifier);
        await monitor.CollectOnce();
        await monitor.CollectOnce();
        Assert.Empty(notifier.Messages);
        await monitor.CollectOnce();
        await monitor.CollectOnce();
        Assert.Single(notifier.Messages);
        agent.ThrowOnStatus.Remove(node.Name);
        agent.Statuses[node.Name] = Status();
        await monitor.CollectOnce();
        await monitor.CollectOnce();
        Assert.Equal(2, notifier.Messages.Count);
    }

    [Fact]
    public async Task User_summary_contains_only_new_expired_or_over_limit_users()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        fixture.Clock.SetUtcNow(new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));
        fixture.Db.ClientUsers.Add(new ClientUser { Name = "expired", SubscriptionToken = "1", ExpiresAt = fixture.Clock.GetUtcNow().UtcDateTime.AddDays(-1) });
        fixture.Db.ClientUsers.Add(new ClientUser { Name = "limit", SubscriptionToken = "2", TrafficLimitBytes = 1, UsedUploadBytes = 1 });
        fixture.Db.ClientUsers.Add(new ClientUser
        {
            Name = "old",
            SubscriptionToken = "3",
            ExpiresAt = fixture.Clock.GetUtcNow().UtcDateTime.AddDays(-1),
            ExpiredNotified = true
        });
        await fixture.Db.SaveChangesAsync();
        var notifier = new RecordingNotifier();
        var monitor = new NodeMonitor(fixture.Db, new FakeAgentClient(), fixture.Clock, notifier);
        await monitor.CollectOnce();
        await monitor.CollectOnce();
        Assert.Single(notifier.Messages);
        Assert.Contains("expired", notifier.Messages[0], StringComparison.Ordinal);
        Assert.Contains("limit", notifier.Messages[0], StringComparison.Ordinal);
        Assert.DoesNotContain("old", notifier.Messages[0], StringComparison.Ordinal);
    }

    private static Node AddNode(ServiceFixture fixture)
    {
        var keys = RealityKeys.Generate();
        var node = new Node
        {
            Name = "node", Address = "127.0.0.1", Country = "RU", Role = NodeRole.Entry,
            RealityPrivateKey = keys.PrivateKey, RealityPublicKey = keys.PublicKey,
            RealityShortId = "abcdef12", AgentUrl = "https://localhost", AgentToken = "token",
            AgentCertSha256 = "cert"
        };
        fixture.Db.Nodes.Add(node);
        return node;
    }

    private static AgentStatus Status()
    {
        var component = new ComponentStatus(true, true, "1");
        return new AgentStatus("1", "host", 1, 0, 1, 1, component, component, component, new OpenFluxStatus(false, []), 1, 1);
    }

    private sealed class RecordingNotifier : ITelegramNotifier
    {
        public List<string> Messages { get; } = [];
        public Task<bool> Send(string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(true);
        }
    }

    private sealed class CountingHttpClientFactory : IHttpClientFactory
    {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Calls++;
            return new HttpClient();
        }
    }

    private sealed class HandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? RequestUri { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri?.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8) };
        }
    }
}
