using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class ChannelLinkParserTests
{
    [Theory]
    [InlineData("https://docs.yandex.ru/edit/d/abcdefghijklmnop", "VyandexUrl")]
    [InlineData("https://disk.yandex.ru/i/abcdef", "YandexUrl")]
    [InlineData("https://cloud.mail.ru/public/ab/cdef", "MailruUrl")]
    [InlineData("https://telemost.yandex.ru/j/123456", "TelemostUrl")]
    [InlineData("https://boards.yandex.ru/whiteboard/?hash=0123456789abcdef0123456789abcdef", "BoardsUrl")]
    [InlineData("avito:abcdefgh", "AvitoUrl")]
    [InlineData("cupsonline:abcdefgh", "CupsUrl")]
    [InlineData("max:123456:token-value", "MaxToken")]
    public void Parses_supported_link(string link, string property)
    {
        var result = ChannelLinkParser.Parse(link);

        Assert.Empty(result.Errors);
        Assert.NotNull(typeof(ChannelLinkParseResult).GetProperty(property)!.GetValue(result));
    }

    [Fact]
    public void Parses_mixed_separators()
    {
        var links = "https://docs.yandex.ru/edit/d/abcdefghijklmnop,\n"
            + "https://cloud.mail.ru/public/ab/cdef https://telemost.yandex.ru/j/123456";

        var result = ChannelLinkParser.Parse(links);

        Assert.Empty(result.Errors);
        Assert.NotNull(result.VyandexUrl);
        Assert.NotNull(result.MailruUrl);
        Assert.NotNull(result.TelemostUrl);
    }

    [Fact]
    public void Reports_unknown_link()
    {
        var result = ChannelLinkParser.Parse("https://example.com/link");

        Assert.Equal(["Неподдерживаемая ссылка: https://example.com/link"], result.Errors);
    }

    [Fact]
    public void Reports_duplicate_transport_type()
    {
        var links = "https://telemost.yandex.ru/j/123456 https://telemost.yandex.ru/j/654321";

        var result = ChannelLinkParser.Parse(links);

        Assert.Equal(["Повторяется ссылка: https://telemost.yandex.ru/j/654321"], result.Errors);
    }

    [Fact]
    public void Second_edit_link_goes_to_yandex_docs()
    {
        var links = "https://docs.yandex.ru/edit/d/abcdefghijklmnop https://docs.yandex.ru/edit/d/qrstuvwxyz123456";

        var result = ChannelLinkParser.Parse(links);

        Assert.Empty(result.Errors);
        Assert.Equal("https://docs.yandex.ru/edit/d/abcdefghijklmnop", result.VyandexUrl);
        Assert.Equal("https://docs.yandex.ru/edit/d/qrstuvwxyz123456", result.YandexUrl);
    }

    [Fact]
    public void Parses_max_link_with_uid()
    {
        var result = ChannelLinkParser.Parse("max:123456:token-value");

        Assert.Empty(result.Errors);
        Assert.Equal(123456, result.MaxUid);
        Assert.Equal("token-value", result.MaxToken);
    }
}
