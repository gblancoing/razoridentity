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

    public Task<IReadOnlyList<Professional>?> GetProfessionalsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Professional>>("/v1/professionals", cancellationToken);

    public Task<PartnerDto?> CreatePartnerAsync(PartnerCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<PartnerDto>("/v1/partners", request, cancellationToken);

    public Task<Product?> CreateProductAsync(ProductCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Product>("/v1/products", request, cancellationToken);

    public Task<Service?> CreateServiceAsync(ServiceCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Service>("/v1/services", request, cancellationToken);

    public Task<Professional?> CreateProfessionalAsync(ProfessionalCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Professional>("/v1/professionals", request, cancellationToken);

    public Task<PartnerDto?> GetPartnerAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<PartnerDto>($"/v1/partners/{partnerId}", cancellationToken);

    public Task<PartnerActivationStatus?> GetActivationStatusAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<PartnerActivationStatus>($"/v1/partners/{partnerId}/activation", cancellationToken);

    public Task<PartnerDto?> UpdateVisibilityAsync(Guid partnerId, bool isVisible, CancellationToken cancellationToken = default)
        => PatchAsync<PartnerDto>($"/v1/partners/{partnerId}/visibility", new { isVisible }, cancellationToken);
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

public sealed record Professional(
    Guid Id,
    Guid TenantId,
    string? Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool IsVerified,
    bool IsActive,
    DateTimeOffset CreatedAt
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

public sealed record PartnerDto(
    Guid Id,
    Guid TenantId,
    Guid? CategoryId,
    string? CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    string? Type,
    string? Name,
    string? Rut,
    string? Address,
    string? Phone,
    string? Email,
    bool IsVisible,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PartnerActivationStatus? Activation
);

public sealed record PartnerActivationStatus(
    Guid PartnerId,
    string PartnerType,
    bool IsVisible,
    bool CanPublish,
    int CompletionPercent,
    string Status,
    string StatusLabel,
    string NextStep,
    IReadOnlyList<PartnerChecklistItem>? Checklist
);

public sealed record PartnerChecklistItem(
    string Key,
    string Label,
    bool IsComplete,
    string? Hint
);

public sealed record PartnerCreateRequest(
    string Type,
    string Name,
    string? Rut,
    string? Address,
    string? Phone,
    string? Email,
    Guid? SubcategoryId
);

public sealed record ProductCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    bool? IsActive
);

public sealed record ServiceCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    int? DurationMinutes,
    bool? IsActive
);

public sealed record ProfessionalCreateRequest(
    string Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? IsVerified,
    bool? IsActive
);
