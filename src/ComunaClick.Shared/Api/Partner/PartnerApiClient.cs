using ComunaClick.Shared.Http;

namespace ComunaClick.Shared.Api.Partner;

public sealed class PartnerApiClient : ApiClientBase
{
    public PartnerApiClient(HttpClient httpClient, Auth.Interfaces.ITokenStore tokenStore, Auth.Interfaces.IAuthClient authClient)
        : base(httpClient, tokenStore, authClient)
    {
    }

    public Task<IReadOnlyList<Order>?> GetPartnerOrdersAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Order>>($"/v1/partners/{partnerId}/orders", cancellationToken);

    public Task<IReadOnlyList<Booking>?> GetPartnerBookingsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Booking>>($"/v1/partners/{partnerId}/bookings", cancellationToken);

    public Task<IReadOnlyList<PayoutItem>?> GetPartnerPayoutsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PayoutItem>>($"/v1/partners/{partnerId}/payouts", cancellationToken);

    public Task<IReadOnlyList<Product>?> GetPartnerProductsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Product>>($"/v1/partners/{partnerId}/products", cancellationToken);

    public Task<IReadOnlyList<Service>?> GetPartnerServicesAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Service>>($"/v1/partners/{partnerId}/services", cancellationToken);

    public Task<IReadOnlyList<Lead>?> GetProfessionalLeadsAsync(Guid professionalId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Lead>>($"/v1/professionals/{professionalId}/leads", cancellationToken);

    public Task<IReadOnlyList<Notification>?> GetPartnerNotificationsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Notification>>($"/v1/partners/{partnerId}/notifications", cancellationToken);
}

public sealed record Product(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    string? Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record Service(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    string? Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    int DurationMinutes,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record Order(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    Guid CustomerId,
    string? Status,
    double Subtotal,
    double DeliveryFee,
    double TotalAmount,
    string? Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<OrderItem>? Items
);

public sealed record OrderItem(
    Guid Id,
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    double UnitPrice,
    double TotalPrice
);

public sealed record Booking(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    Guid ServiceId,
    Guid? SlotId,
    Guid CustomerId,
    string? Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    double Amount,
    string? Currency,
    string? CancellationPolicy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record PayoutItem(
    Guid Id,
    Guid BatchId,
    Guid PartnerId,
    double GrossAmount,
    double CommissionAmount,
    double SubscriptionDeduction,
    double NetAmount,
    string? Currency,
    DateTimeOffset CreatedAt
);

public sealed record Lead(
    Guid Id,
    Guid TenantId,
    Guid ProfessionalId,
    Guid CustomerId,
    string? Status,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record Notification(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    Guid? PartnerId,
    string? Type,
    Guid? ReferenceId,
    string? Payload,
    DateTimeOffset CreatedAt
);
