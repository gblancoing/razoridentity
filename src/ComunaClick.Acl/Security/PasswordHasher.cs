using System.Security.Cryptography;

namespace ComunaClick.Acl.Security;

public static class PasswordHasher
{
    private const string Prefix = "pbkdf2";
    private const int DefaultIterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    public static string Hash(string password, int? iterations = null)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var iter = iterations ?? DefaultIterations;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iter, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}:{iter}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash))
        {
            return false;
        }

        if (storedHash.StartsWith(Prefix + ":", StringComparison.OrdinalIgnoreCase))
        {
            return VerifyPbkdf2(password, storedHash);
        }

        if (IsSha256Hex(storedHash))
        {
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)));
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(hash),
                System.Text.Encoding.UTF8.GetBytes(storedHash));
        }

        return string.Equals(password, storedHash, StringComparison.Ordinal);
    }

    private static bool VerifyPbkdf2(string password, string storedHash)
    {
        var parts = storedHash.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static bool IsSha256Hex(string value)
    {
        if (value.Length != 64)
        {
            return false;
        }

        foreach (var ch in value)
        {
            var isHex = (ch >= '0' && ch <= '9') || (ch >= 'A' && ch <= 'F') || (ch >= 'a' && ch <= 'f');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}
