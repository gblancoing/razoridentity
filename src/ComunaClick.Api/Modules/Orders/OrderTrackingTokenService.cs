using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Orders;

public interface IOrderTrackingTokenService
{
    /// <summary>Crea un token firmado y con expiración para seguir una orden públicamente.</summary>
    string Create(Guid orderId, Guid customerId);

    /// <summary>
    /// Valida el token contra la orden esperada. Devuelve el customerId embebido si el token
    /// es válido (firma correcta y no expirado).
    /// </summary>
    bool TryValidate(string? token, Guid expectedOrderId, out Guid customerId);
}

public sealed class OrderTrackingTokenService : IOrderTrackingTokenService
{
    private readonly byte[] _key;
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _timeProvider;

    public OrderTrackingTokenService(IOptions<OrderTrackingOptions> options, TimeProvider timeProvider)
    {
        var secret = options.Value.TrackingTokenSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "OrderTrackingOptions.TrackingTokenSecret no está configurada (ni Jwt:SigningKey como respaldo).");
        }

        _key = Encoding.UTF8.GetBytes(secret);
        _ttl = TimeSpan.FromDays(Math.Max(1, options.Value.TrackingTokenTtlDays));
        _timeProvider = timeProvider;
    }

    public string Create(Guid orderId, Guid customerId)
    {
        var expiresAt = _timeProvider.GetUtcNow().Add(_ttl).ToUnixTimeSeconds();
        var payload = $"{orderId:N}.{customerId:N}.{expiresAt}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = ComputeSignature(payloadBytes);
        return $"{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    public bool TryValidate(string? token, Guid expectedOrderId, out Guid customerId)
    {
        customerId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        byte[] payloadBytes;
        byte[] providedSignature;
        try
        {
            payloadBytes = FromBase64Url(parts[0]);
            providedSignature = FromBase64Url(parts[1]);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedSignature = ComputeSignature(payloadBytes);
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(payloadBytes);
        var fields = payload.Split('.');
        if (fields.Length != 3
            || !Guid.TryParseExact(fields[0], "N", out var orderId)
            || !Guid.TryParseExact(fields[1], "N", out var parsedCustomerId)
            || !long.TryParse(fields[2], out var expiresAtUnix))
        {
            return false;
        }

        if (orderId != expectedOrderId)
        {
            return false;
        }

        if (_timeProvider.GetUtcNow().ToUnixTimeSeconds() > expiresAtUnix)
        {
            return false;
        }

        customerId = parsedCustomerId;
        return true;
    }

    private byte[] ComputeSignature(byte[] payloadBytes)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(payloadBytes);
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };
        return Convert.FromBase64String(padded);
    }
}
