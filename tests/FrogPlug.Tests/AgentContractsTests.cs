using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class AgentContractsTests
{
    [Fact]
    public void Tunnel_serializer_round_trips_request()
    {
        var request = new TunnelRequest("id", "POST", "/status", "{\"a\":1}");
        var json = TunnelSerializer.Serialize(request);
        Assert.Equal(request, TunnelSerializer.Deserialize<TunnelRequest>(json));
    }

    [Fact]
    public void Tunnel_serializer_rejects_message_larger_than_limit()
    {
        var json = new string('x', TunnelSerializer.MaxMessageBytes + 1);
        Assert.Throws<InvalidOperationException>(() => TunnelSerializer.ToUtf8(json));
    }

    [Fact]
    public void Tunnel_serializer_round_trips_response()
    {
        var response = new TunnelResponse("id", 200, "{\"ok\":true}", null);
        var json = TunnelSerializer.Serialize(response);
        Assert.Equal(response, TunnelSerializer.Deserialize<TunnelResponse>(json));
    }

    [Fact]
    public void Tunnel_serializer_round_trips_hello()
    {
        var hello = new TunnelHello("1.2.3", "X64");
        var json = TunnelSerializer.Serialize(hello);
        Assert.Equal(hello, TunnelSerializer.Deserialize<TunnelHello>(json));
    }

    [Fact]
    public void Tunnel_serializer_accepts_message_at_limit()
    {
        var json = new string('x', TunnelSerializer.MaxMessageBytes);
        Assert.Equal(TunnelSerializer.MaxMessageBytes, TunnelSerializer.ToUtf8(json).Length);
    }

    [Fact]
    public void Tunnel_request_keeps_null_body()
    {
        var request = new TunnelRequest("id", "GET", "/status", null);
        Assert.Null(TunnelSerializer.Deserialize<TunnelRequest>(TunnelSerializer.Serialize(request))?.BodyJson);
    }

    [Fact]
    public void Digest_parser_reads_sha2_marker()
    {
        Assert.Equal(new string('a', 64), AgentParsers.ParseDigest($"SHA2-256={new string('a', 64)}"));
    }

    [Fact]
    public void Digest_parser_accepts_lowercase_marker()
    {
        Assert.Equal(new string('b', 64), AgentParsers.ParseDigest($"sha2-256={new string('b', 64)}"));
    }

    [Fact]
    public void Digest_parser_ignores_short_value()
    {
        Assert.Null(AgentParsers.ParseDigest("SHA2-256=abc"));
    }

    [Fact]
    public void Digest_parser_reads_first_valid_line()
    {
        var text = "invalid\nSHA2-256=" + new string('c', 64) + " trailing";
        Assert.Equal(new string('c', 64), AgentParsers.ParseDigest(text));
    }

    [Fact]
    public void Sha_sum_parser_reads_hash()
    {
        Assert.Equal(new string('d', 64), AgentParsers.ParseSha256Sum(new string('d', 64) + "  file"));
    }

    [Fact]
    public void Sha_sum_parser_rejects_non_hex()
    {
        Assert.Null(AgentParsers.ParseSha256Sum(new string('g', 64) + " file"));
    }

    [Fact]
    public void Stats_parser_reads_string_values()
    {
        var stats = AgentParsers.ParseStats("{\"stat\":[{\"name\":\"user>>>u1>>>traffic>>>uplink\",\"value\":\"7\"}]}");
        Assert.Equal(7, stats.Users["u1"].Up);
    }

    [Fact]
    public void Stats_parser_reads_numeric_values()
    {
        var stats = AgentParsers.ParseStats("{\"stat\":[{\"name\":\"user>>>u1>>>traffic>>>downlink\",\"value\":9}]}");
        Assert.Equal(9, stats.Users["u1"].Down);
    }

    [Fact]
    public void Stats_parser_aggregates_directions()
    {
        var json = "{\"stat\":[" +
            "{\"name\":\"user>>>u1>>>traffic>>>uplink\",\"value\":2}," +
            "{\"name\":\"user>>>u1>>>traffic>>>uplink\",\"value\":3}]}";
        Assert.Equal(5, AgentParsers.ParseStats(json).Users["u1"].Up);
    }

    [Fact]
    public void Stats_parser_ignores_node_emails_for_lookup_only_by_consumer()
    {
        var stats = AgentParsers.ParseStats("{\"stat\":[{\"name\":\"user>>>node2>>>traffic>>>uplink\",\"value\":4}]}");
        Assert.Equal(4, stats.Users["node2"].Up);
    }

    [Fact]
    public void Stats_parser_returns_empty_for_missing_stat()
    {
        Assert.Empty(AgentParsers.ParseStats("{}").Users);
    }

    [Fact]
    public void Stats_parser_ignores_malformed_names()
    {
        var stats = AgentParsers.ParseStats("{\"stat\":[{\"name\":\"bad\",\"value\":4}]}");
        Assert.Empty(stats.Users);
    }

    [Fact]
    public void Stats_parser_is_case_insensitive_for_email_dictionary()
    {
        var stats = AgentParsers.ParseStats("{\"stat\":[{\"name\":\"user>>>U1>>>traffic>>>uplink\",\"value\":4}]}");
        Assert.Equal(4, stats.Users["u1"].Up);
    }
}
