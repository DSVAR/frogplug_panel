using System.Text;
using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class TotpTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32_matches_rfc4648_without_padding(string text, string encoded)
    {
        Assert.Equal(encoded, Totp.Base32Encode(Encoding.ASCII.GetBytes(text)));
        Assert.Equal(text, Encoding.ASCII.GetString(Totp.Base32Decode(encoded)));
    }

    [Theory]
    [InlineData(0L, "755224")]
    [InlineData(1L, "287082")]
    [InlineData(2L, "359152")]
    [InlineData(3L, "969429")]
    [InlineData(4L, "338314")]
    [InlineData(5L, "254676")]
    [InlineData(6L, "287922")]
    [InlineData(7L, "162583")]
    [InlineData(8L, "399871")]
    [InlineData(9L, "520489")]
    public void Generate_matches_rfc4226_six_digit_vectors(long step, string code)
    {
        var secret = Totp.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890"));
        var now = DateTimeOffset.FromUnixTimeSeconds(step * 30);
        Assert.Equal(code, Totp.Generate(secret, now));
    }

    [Theory]
    [InlineData(59L, "94287082")]
    [InlineData(1111111109L, "07081804")]
    [InlineData(1111111111L, "14050471")]
    [InlineData(1234567890L, "89005924")]
    [InlineData(2000000000L, "69279037")]
    [InlineData(20000000000L, "65353130")]
    public void Generate_matches_rfc6238_sha1_vectors(long unixTime, string code)
    {
        var secret = Totp.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890"));
        Assert.Equal(code, Totp.Generate(secret, DateTimeOffset.FromUnixTimeSeconds(unixTime), 8));
    }

    [Fact]
    public void Verify_accepts_one_step_window_only()
    {
        var secret = Totp.Base32Encode(Encoding.ASCII.GetBytes("12345678901234567890"));
        var now = DateTimeOffset.FromUnixTimeSeconds(90);
        Assert.True(Totp.Verify(secret, Totp.Generate(secret, now.AddSeconds(-30)), now));
        Assert.False(Totp.Verify(secret, Totp.Generate(secret, now.AddSeconds(-60)), now));
    }
}
