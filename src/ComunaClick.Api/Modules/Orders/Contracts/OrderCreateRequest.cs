namespace ComunaClick.Api.Modules.Orders.Contracts;

public sealed record OrderCreateRequest(
    Guid PartnerId,
    Guid CustomerId,
    decimal DeliveryFee,
    string? Currency,
    List<OrderItemCreateRequest> Items,
    Guid? DeliveryProviderId = null,
    string? DeliveryAddress = null,
    // Pin de destino fijado por el comprador en el checkout (opcional).
    double? DestinationLat = null,
    double? DestinationLng = null);

public sealed record OrderItemCreateRequest(Guid ProductId, int Quantity, decimal UnitPrice);
