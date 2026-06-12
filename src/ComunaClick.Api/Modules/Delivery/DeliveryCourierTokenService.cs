using System.Security.Cryptography;
using System.Text;
using ComunaClick.Api.Configuration;
using ComunaClick.Api.Modules.Orders;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Delivery;

public interface IDeliveryCourierTokenService
{
    /// <summary>Crea el token firmado del repartidor para una orden. Incluye la
    /// clave de asignación (CourierTokenKey): regenerarla revoca tokens previos.</summary>
    string Create(Guid orderId, string tokenKey);

    /// <summary>
    /// Valida firma, expiración, orden y que la clave embebida coincida con la
    /// clave vigente de la orden (token revocado al reasignar/entregar/cancelar).
    /// </summary>
    bool TryValidate(string? token, Guid expectedOrderId, string? expectedTokenKey);
}

/// <summary>
/// Token HMAC del repartidor. Reutiliza el secreto de tracking de órdenes pero
/// con un payload de propósito distinto ("courier."), de modo que un token de
/// comprador jamás autoriza a reportar GPS ni viceversa.
/// </summary>
public sealed class DeliveryCourierTokenService : IDeliveryCourierTokenService
{
    private const string Purpose = "courier";

    private readonly byte[] _key;
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _timeProvider;

    public DeliveryCourierTokenService(
        IOptions<OrderTrackingOptions> trackingOptions,
        IOptions<DeliveryOptions> deliveryOptions,
        TimeProvider timeProvider)
    {
        var secret = trackingOptions.Value.TrackingTokenSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "OrderTrackingOptions.TrackingTokenSecret no está configurada (requerida también para tokens de repartidor).");
        }

        _key = Encoding.UTF8.GetBytes(secret);
        var hours = Math.Clamp(deliveryOptions.Value.CourierTokenTtlHours, 1, 168);
        _ttl = TimeSpan.FromHours(hours);
        _timeProvider = timeProvider;
    }

    public string Create(Guid orderId, string tokenKey)
    {
        if (string.IsNullOrWhiteSpace(tokenKey))
        {
            throw new InvalidOperationException("La orden no tiene clave de asignación de repartidor.");
        }

        var expiresAt = _timeProvider.GetUtcNow().Add(_ttl).ToUnixTimeSeconds();
        var payload = $"{Purpose}.{orderId:N}.{tokenKey}.{expiresAt}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var signature = ComputeSignature(payloadBytes);
        return $"{Base64Url(payloadBytes)}.{Base64Url(signature)}";
    }

    public bool TryValidate(string? token, Guid expectedOrderId, string? expectedTokenKey)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(expectedTokenKey))
        {
            // Sin clave vigente en la orden no hay token válido (revocado).
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
        if (fields.Length != 4
            || !string.Equals(fields[0], Purpose, StringComparison.Ordinal)
            || !Guid.TryParseExact(fields[1], "N", out var orderId)
            || !long.TryParse(fields[3], out var expiresAtUnix))
        {
            return false;
        }

        if (orderId != expectedOrderId)
        {
            return false;
        }

        // Comparación de la clave de asignación en tiempo constante.
        var providedKey = Encoding.UTF8.GetBytes(fields[2]);
        var expectedKey = Encoding.UTF8.GetBytes(expectedTokenKey);
        if (providedKey.Length != expectedKey.Length
            || !CryptographicOperations.FixedTimeEquals(providedKey, expectedKey))
        {
            return false;
        }

        return _timeProvider.GetUtcNow().ToUnixTimeSeconds() <= expiresAtUnix;
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
