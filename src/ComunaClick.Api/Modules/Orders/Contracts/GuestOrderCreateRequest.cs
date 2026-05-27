namespace ComunaClick.Api.Modules.Orders.Contracts;

public sealed record GuestContactRequest(
    string FullName,
    string Email,
    string? Phone);

public sealed record GuestOrderCreateRequest(
    Guid TenantId,
    Guid PartnerId,
    IReadOnlyList<OrderItemCreateRequest> Items,
    GuestContactRequest Guest,
    decimal DeliveryFee = 0,
    string? Currency = null,
    string? DeliveryAddress = null);

public sealed record GuestOrderCreateResponse(
    Guid OrderId,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string Currency);
