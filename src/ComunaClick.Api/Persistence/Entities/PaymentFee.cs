namespace ComunaClick.Api.Persistence.Entities;

public sealed class PaymentFee
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public decimal PlatformFeeAmount { get; set; }
    public decimal? MercadoPagoFeeAmount { get; set; }
    public decimal NetToSellerAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
