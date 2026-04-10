namespace ComunaClick.Api.Modules.Cart.Contracts;

public sealed record CartItemUpsertRequest(
    Guid ProductId,
    int Quantity);

public sealed record CartCheckoutRequest(
    Guid CartId,
    Guid? DeliveryProviderId,
    string? DeliveryAddress,
    decimal? DeliveryFee,
    string? Currency);
