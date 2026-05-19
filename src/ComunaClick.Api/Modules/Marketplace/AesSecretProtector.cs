using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class AesSecretProtector : ISecretProtector
{
    private readonly byte[] _keyBytes;

    public AesSecretProtector(IOptions<MercadoPagoMarketplaceOptions> options)
    {
        var key = options.Value.EncryptionKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Marketplace encryption key is required.");
        }

        _keyBytes = ParseKey(key);
    }

    public string Protect(string plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return string.Empty;
        }

        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipherBytes = new byte[plaintextBytes.Length];

        using var aes = new AesGcm(_keyBytes, 16);
        aes.Encrypt(nonce, plaintextBytes, cipherBytes, tag);

        return Convert.ToBase64String(nonce.Concat(tag).Concat(cipherBytes).ToArray());
    }

    public string Unprotect(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return string.Empty;
        }

        var payload = Convert.FromBase64String(cipherText);
        var nonce = payload[..12];
        var tag = payload[12..28];
        var cipherBytes = payload[28..];
        var plaintextBytes = new byte[cipherBytes.Length];

        using var aes = new AesGcm(_keyBytes, 16);
        aes.Decrypt(nonce, cipherBytes, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static byte[] ParseKey(string key)
    {
        try
        {
            var decoded = Convert.FromBase64String(key);
            if (decoded.Length is 16 or 24 or 32)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
            // Fall through to UTF8 bytes if key is not base64.
        }

        var bytes = Encoding.UTF8.GetBytes(key);
        if (bytes.Length >= 32)
        {
            return bytes[..32];
        }

        return bytes.Concat(new byte[32 - bytes.Length]).ToArray();
    }
}
