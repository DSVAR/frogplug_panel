using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class RealityKeysTests
{
    [Fact]
    public void Alice_vector_matches_rfc7748()
    {
        const string privateKey = "77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a";
        const string expected = "8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a";
        Assert.Equal(expected, Hex(RealityKeys.PublicKeyFromPrivate(Base64Url(privateKey))));
    }

    [Fact]
    public void Bob_vector_matches_rfc7748()
    {
        const string privateKey = "5dab087e624a8a4b79e17f8b83800ee66f3bb1292618b6fd1c2f8b27ff88e0eb";
        const string expected = "de9edb7d7b7dc1b4d35b61c2ece435373f8343c85b78674dadfc7e146f882b4f";
        var actual = RealityKeys.PublicKeyFromPrivate(Base64Url(privateKey));
        Assert.Equal(expected, Hex(actual));
    }

    [Fact]
    public void Generate_returns_43_character_keys()
    {
        var pair = RealityKeys.Generate();
        Assert.Equal(43, pair.PrivateKey.Length);
        Assert.Equal(43, pair.PublicKey.Length);
    }

    [Fact]
    public void PublicKey_roundtrip_is_stable()
    {
        var pair = RealityKeys.Generate();
        Assert.Equal(pair.PublicKey, RealityKeys.PublicKeyFromPrivate(pair.PrivateKey));
    }

    private static string Base64Url(string hex)
    {
        return Convert.ToBase64String(Convert.FromHexString(hex))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string Hex(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/').PadRight((value.Length + 3) / 4 * 4, '=');
        return Convert.ToHexString(Convert.FromBase64String(padded)).ToLowerInvariant();
    }
}






