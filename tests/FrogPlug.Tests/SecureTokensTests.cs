using System.Text.RegularExpressions;
using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class SecureTokensTests
{
    [Fact]
    public void Base64Url_has_expected_length_and_charset()
    {
        var value = SecureTokens.RandomBase64Url(32);
        Assert.Equal(43, value.Length);
        Assert.Matches("^[A-Za-z0-9_-]+$", value);
    }

    [Fact]
    public void Hex_has_expected_length_and_charset()
    {
        var value = SecureTokens.RandomHex(4);
        Assert.Equal(8, value.Length);
        Assert.Matches("^[0-9a-f]+$", value);
    }

    [Fact]
    public void Invalid_size_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SecureTokens.RandomHex(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SecureTokens.RandomBase64Url(-1));
    }
}
