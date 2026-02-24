namespace ComunaClick.Api.Modules.Orders.Contracts;

public sealed record OrderCreateRequest(
    Guid PartnerId,
    Guid CustomerId,
    decimal DeliveryFee,
    string? Currency,
    List<OrderItemCreateRequest> Items);

public sealed record OrderItemCreateRequest(Guid ProductId, int Quantity, decimal UnitPrice);
