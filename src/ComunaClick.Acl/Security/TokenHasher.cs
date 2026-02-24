using System.Security.Cryptography;
using System.Text;

namespace ComunaClick.Acl.Security;

public static class TokenHasher
{
    public static string Hash(string token, string? pepper)
    {
        var input = string.IsNullOrWhiteSpace(pepper) ? token : $"{pepper}:{token}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }
}
