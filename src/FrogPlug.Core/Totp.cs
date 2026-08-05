using System.Security.Cryptography;
using System.Text;

namespace FrogPlug.Core;

public static class Totp
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int StepSeconds = 30;

    public static string GenerateSecret(int byteLength = 20)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(byteLength, 1);
        return Base32Encode(RandomNumberGenerator.GetBytes(byteLength));
    }

    public static string Generate(string secret, DateTimeOffset now, int digits = 6)
    {
        return Generate(Base32Decode(secret), GetStep(now), digits);
    }

    public static bool Verify(string secret, string code, DateTimeOffset now, int window = 1)
    {
        return TryVerify(secret, code, now, window, out _);
    }

    public static bool TryVerify(string secret, string code, DateTimeOffset now, int window, out long matchedStep)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(window, 0);
        matchedStep = -1;
        byte[] key;
        try
        {
            key = Base32Decode(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var currentStep = GetStep(now);
        for (var offset = -window; offset <= window; offset++)
        {
            var expected = Generate(key, currentStep + offset, 6);
            if (FixedTimeEquals(expected, code))
            {
                matchedStep = currentStep + offset;
                return true;
            }
        }

        return false;
    }

    public static long GetStep(DateTimeOffset now)
    {
        return now.ToUnixTimeSeconds() / StepSeconds;
    }

    public static string ProvisioningUri(string issuer, string login, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        var label = Uri.EscapeDataString($"{issuer}:{login}");
        return $"otpauth://totp/{label}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";
    }

    public static string Base32Encode(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return string.Empty;
        }

        var output = new StringBuilder((value.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var item in value)
        {
            buffer = (buffer << 8) | item;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                output.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            output.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return output.ToString();
    }

    public static byte[] Base32Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new List<byte>(value.Length * 5 / 8);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var character in value)
        {
            if (character is ' ' or '-')
            {
                continue;
            }

            if (character == '=')
            {
                break;
            }

            var index = Base32Alphabet.IndexOf(char.ToUpperInvariant(character));
            if (index < 0)
            {
                throw new FormatException("Invalid Base32 character");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                result.Add((byte)(buffer >> (bitsLeft - 8)));
                bitsLeft -= 8;
            }
        }

        return [.. result];
    }

    private static string Generate(ReadOnlySpan<byte> key, long step, int digits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(digits, 1);
        var counter = new byte[8];
        for (var index = counter.Length - 1; index >= 0; index--)
        {
            counter[index] = (byte)step;
            step >>= 8;
        }

        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24)
            | (hash[offset + 1] << 16)
            | (hash[offset + 2] << 8)
            | hash[offset + 3];
        return (binary % (int)Math.Pow(10, digits)).ToString($"D{digits}");
    }

    private static bool FixedTimeEquals(string expected, string? actual)
    {
        if (actual is null || expected.Length != actual.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(actual));
    }
}
