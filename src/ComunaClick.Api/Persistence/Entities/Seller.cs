namespace ComunaClick.Api.Persistence.Entities;

public sealed class Seller
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public SellerMercadoPagoAccount? MercadoPagoAccount { get; set; }
    public SellerFeeConfiguration? FeeConfiguration { get; set; }
}
