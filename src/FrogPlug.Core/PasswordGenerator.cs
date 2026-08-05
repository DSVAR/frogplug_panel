using System.Security.Cryptography;

namespace FrogPlug.Core;

public static class PasswordGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_=+";

    public static string Generate(int length = 20)
    {
        if (length < 12)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        var password = new char[length];
        for (var index = 0; index < password.Length; index++)
        {
            password[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(password);
    }
}
