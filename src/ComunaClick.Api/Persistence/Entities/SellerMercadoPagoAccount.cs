namespace ComunaClick.Api.Persistence.Entities;

public sealed class SellerMercadoPagoAccount
{
    public Guid Id { get; set; }
    public Guid SellerId { get; set; }
    public string? MpUserId { get; set; }
    public string AccessTokenEncrypted { get; set; } = string.Empty;
    public string? RefreshTokenEncrypted { get; set; }
    public DateTimeOffset? TokenExpiresAt { get; set; }
    public string? Scope { get; set; }
    public string ConnectionStatus { get; set; } = "disconnected";
    public DateTimeOffset? ConnectedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Seller Seller { get; set; } = null!;
}
