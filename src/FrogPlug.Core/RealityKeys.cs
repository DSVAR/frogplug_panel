using System.Numerics;
using System.Security.Cryptography;

namespace FrogPlug.Core;

public sealed record RealityKeyPair(string PrivateKey, string PublicKey);

public static class RealityKeys
{
    private static readonly BigInteger Prime = (BigInteger.One << 255) - 19;
    private const int MontgomeryA24 = 121665;

    public static RealityKeyPair Generate()
    {
        var privateBytes = RandomNumberGenerator.GetBytes(32);
        var publicBytes = ScalarMultiply(privateBytes, 9);
        return new RealityKeyPair(Encode(privateBytes), Encode(publicBytes));
    }

    public static string PublicKeyFromPrivate(string privateBase64Url)
    {
        var privateBytes = Decode(privateBase64Url);
        if (privateBytes.Length != 32)
        {
            throw new ArgumentException("Приватный ключ Reality должен содержать 32 байта", nameof(privateBase64Url));
        }

        return Encode(ScalarMultiply(privateBytes, 9));
    }

    private static byte[] ScalarMultiply(byte[] scalar, byte uCoordinate)
    {
        var clampedScalar = (byte[])scalar.Clone();
        clampedScalar[0] &= 248;
        clampedScalar[31] &= 127;
        clampedScalar[31] |= 64;

        var x1 = new BigInteger(uCoordinate);
        var x2 = BigInteger.One;
        var z2 = BigInteger.Zero;
        var x3 = x1;
        var z3 = BigInteger.One;
        var swap = 0;

        for (var bit = 254; bit >= 0; bit--)
        {
            var scalarBit = (clampedScalar[bit / 8] >> (bit % 8)) & 1;
            swap ^= scalarBit;
            if (swap == 1)
            {
                (x2, x3) = (x3, x2);
                (z2, z3) = (z3, z2);
            }

            swap = scalarBit;
            var a = Mod(x2 + z2);
            var aa = Mod(a * a);
            var b = Mod(x2 - z2);
            var bb = Mod(b * b);
            var e = Mod(aa - bb);
            var c = Mod(x3 + z3);
            var d = Mod(x3 - z3);
            var da = Mod(d * a);
            var cb = Mod(c * b);

            x3 = Mod((da + cb) * (da + cb));
            z3 = Mod(x1 * (da - cb) * (da - cb));
            x2 = Mod(aa * bb);
            z2 = Mod(e * (aa + MontgomeryA24 * e));
        }

        if (swap == 1)
        {
            (x2, x3) = (x3, x2);
            (z2, z3) = (z3, z2);
        }

        var result = Mod(x2 * BigInteger.ModPow(z2, Prime - 2, Prime));
        return ToLittleEndian(result, 32);
    }

    private static BigInteger Mod(BigInteger value)
    {
        var result = value % Prime;
        return result < 0 ? result + Prime : result;
    }

    private static byte[] ToLittleEndian(BigInteger value, int length)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: false);
        Array.Resize(ref bytes, length);
        return bytes;
    }

    private static string Encode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight((padded.Length + 3) / 4 * 4, '=');
        try
        {
            return Convert.FromBase64String(padded);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Некорректный формат ключа Reality", nameof(value), exception);
        }
    }
}
