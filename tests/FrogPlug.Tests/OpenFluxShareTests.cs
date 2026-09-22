using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class OpenFluxShareTests
{
    [Fact]
    public void Encode_produces_openflux_v1_payload_for_multi_transport_vector()
    {
        // share_test.go v0.17.1 has no literal Encode vectors. This recreates its
        // sample() configuration and validates the wire JSON after raw DEFLATE.
        var link = OpenFluxShare.Encode(new OpenFluxShareConfig("VDS", true, null,
            "a shared secret of 32 characters", "https://disk.yandex.ru/i/abc",
            [new OpenFluxTransport("direct", Priority: 100, Dial: "203.0.113.7:8445"),
             new OpenFluxTransport("yandex", Url: "https://disk.yandex.ru/i/abc", Priority: 50)]));
        using var payload = JsonDocument.Parse(Inflate(link));
        Assert.Equal("VDS", payload.RootElement.GetProperty("name").GetString());
        Assert.True(payload.RootElement.GetProperty("negotiate").GetBoolean());
        Assert.Equal("direct", payload.RootElement.GetProperty("transports")[0].GetProperty("type").GetString());
    }

    [Fact]
    public void Encode_reproduces_telemost_vector_shape()
    {
        // The other share_test.go Encode case is an unencrypted single Telemost carrier.
        var link = OpenFluxShare.Encode(new OpenFluxShareConfig("Телемост", false, null, null, null,
            [new OpenFluxTransport("telemost", Url: "https://telemost.yandex.ru/j/88936358186517")]));
        using var payload = JsonDocument.Parse(Inflate(link));
        Assert.False(payload.RootElement.TryGetProperty("secret", out _));
        Assert.Equal("telemost", payload.RootElement.GetProperty("transports")[0].GetProperty("type").GetString());
    }

    [Theory]
    [InlineData("channel-1", true)]
    [InlineData("-channel", false)]
    [InlineData("UPPER", false)]
    [InlineData("a1234567890123456789012345678901", false)]
    public void Channel_validation_matches_installer(string value, bool expected)
    {
        Assert.Equal(expected, OpenFluxChannelValidator.IsChannel(value));
    }

    [Theory]
    [InlineData("https://docs.yandex.ru/edit/d/abcdefghijklmnop", true)]
    [InlineData("https://disk.yandex.com/i/abcdef", true)]
    [InlineData("https://cloud.mail.ru/public/ab/cdef", true)]
    [InlineData("https://telemost.yandex.ru/j/123456", true)]
    [InlineData("https://boards.yandex.ru/whiteboard/?hash=0123456789abcdef0123456789abcdef", true)]
    [InlineData("not-a-url", false)]
    public void Carrier_validation_matches_installer(string value, bool expected)
    {
        var valid = OpenFluxChannelValidator.IsYandexUrl(value)
            || OpenFluxChannelValidator.IsMailruUrl(value)
            || OpenFluxChannelValidator.IsTelemostUrl(value)
            || OpenFluxChannelValidator.IsBoardsUrl(value);
        Assert.Equal(expected, valid);
    }

    private static string Inflate(string link)
    {
        var body = link["openflux://v1/".Length..].Replace('-', '+').Replace('_', '/');
        body = body.PadRight(body.Length + (4 - body.Length % 4) % 4, '=');
        using var input = new MemoryStream(Convert.FromBase64String(body));
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(deflate, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
