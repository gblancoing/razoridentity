namespace ComunaClick.Api.Modules.Marketplace.Contracts;

public sealed record UpdateSellerFeesRequest(
    decimal FixedFeeAmount,
    decimal PercentageFee,
    bool IsActive = true);
