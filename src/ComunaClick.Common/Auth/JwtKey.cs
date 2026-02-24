using System.Text;

namespace ComunaClick.Common.Auth;

public static class JwtKey
{
    public static byte[] GetKeyBytes(string signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("Jwt:SigningKey is required.");
        }

        var trimmed = signingKey.Trim();

        try
        {
            var decoded = Convert.FromBase64String(trimmed);
            if (decoded.Length >= 16)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
            // Not base64, fall back to raw string bytes.
        }

        var raw = Encoding.UTF8.GetBytes(trimmed);
        if (raw.Length < 16)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be at least 16 bytes (128 bits).");
        }

        return raw;
    }
}
