using System.Security.Cryptography;

namespace PbHtmlBuilder.Domain.Ids;

public static class StableIdGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_";

    public static string NewId(string prefix, int length = 10)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var chars = new char[length];
        var bytes = RandomNumberGenerator.GetBytes(length);

        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = Alphabet[bytes[index] % Alphabet.Length];
        }

        return $"{prefix}_{new string(chars)}";
    }
}
