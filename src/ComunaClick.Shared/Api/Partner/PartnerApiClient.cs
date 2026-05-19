using ComunaClick.Shared.Http;

namespace ComunaClick.Shared.Api.Partner;

public sealed class PartnerApiClient : ApiClientBase
{
    public PartnerApiClient(HttpClient httpClient, Auth.Interfaces.ITokenStore tokenStore, Auth.Interfaces.IAuthClient authClient)
        : base(httpClient, tokenStore, authClient)
    {
    }

    public Task<IReadOnlyList<PartnerDto>?> ListPartnersAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PartnerDto>>("/v1/partners", cancellationToken);

    public async Task<IReadOnlyList<PartnerDto>?> ListMyPartnersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync<IReadOnlyList<PartnerDto>>("/v1/partners/mine", cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return await GetAsync<IReadOnlyList<PartnerDto>>("/v1/partners/list-mine", cancellationToken);
        }
    }

    public Task<IReadOnlyList<Order>?> GetPartnerOrdersAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Order>>($"/v1/partners/{partnerId}/orders", cancellationToken);

    public Task<IReadOnlyList<Booking>?> GetPartnerBookingsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Booking>>($"/v1/partners/{partnerId}/bookings", cancellationToken);

    public Task<Booking?> UpdateBookingStatusAsync(Guid bookingId, BookingStatusUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Booking>($"/v1/bookings/{bookingId}/status", request, cancellationToken);

    public Task<Booking?> UpdateBookingWorkflowAsync(Guid bookingId, BookingWorkflowUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Booking>($"/v1/bookings/{bookingId}/workflow", request, cancellationToken);

    public Task<IReadOnlyList<PayoutItem>?> GetPartnerPayoutsAsync(
        Guid partnerId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        string? batchStatus = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>();
        if (from.HasValue)
        {
            query.Add($"from={Uri.EscapeDataString(from.Value.ToString("O"))}");
        }

        if (to.HasValue)
        {
            query.Add($"to={Uri.EscapeDataString(to.Value.ToString("O"))}");
        }

        if (!string.IsNullOrWhiteSpace(batchStatus))
        {
            query.Add($"batchStatus={Uri.EscapeDataString(batchStatus.Trim())}");
        }

        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        return GetAsync<IReadOnlyList<PayoutItem>>($"/v1/partners/{partnerId}/payouts{suffix}", cancellationToken);
    }

    public Task<IReadOnlyList<Product>?> GetPartnerProductsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Product>>($"/v1/partners/{partnerId}/products", cancellationToken);

    public Task<IReadOnlyList<Service>?> GetPartnerServicesAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Service>>($"/v1/partners/{partnerId}/services", cancellationToken);

    public Task<IReadOnlyList<Professional>?> GetPartnerProfessionalsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Professional>>($"/v1/partners/{partnerId}/professionals", cancellationToken);

    public Task<IReadOnlyList<Lead>?> GetPartnerLeadsAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Lead>>($"/v1/partners/{partnerId}/leads", cancellationToken);

    public Task<IReadOnlyList<Lead>?> GetProfessionalLeadsAsync(Guid professionalId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Lead>>($"/v1/professionals/{professionalId}/leads", cancellationToken);

    public Task<Lead?> UpdateLeadStatusAsync(Guid leadId, LeadStatusUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Lead>($"/v1/leads/{leadId}/status", request, cancellationToken);

    public Task<Lead?> UpdateLeadWorkflowAsync(Guid leadId, LeadWorkflowUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Lead>($"/v1/leads/{leadId}/workflow", request, cancellationToken);

    public Task<IReadOnlyList<Notification>?> GetPartnerNotificationsAsync(Guid partnerId, bool includeArchived = false, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Notification>>($"/v1/partners/{partnerId}/notifications?includeArchived={includeArchived.ToString().ToLowerInvariant()}", cancellationToken);

    public Task<Notification?> MarkNotificationReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => PatchAsync<Notification>($"/v1/notifications/{notificationId}/read", new { }, cancellationToken);

    public Task<Notification?> ArchiveNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default)
        => PatchAsync<Notification>($"/v1/notifications/{notificationId}/archive", new { }, cancellationToken);

    public Task<IReadOnlyList<Professional>?> GetProfessionalsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<Professional>>("/v1/professionals", cancellationToken);

    public Task<PartnerDto?> CreatePartnerAsync(PartnerCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<PartnerDto>("/v1/partners", request, cancellationToken);

    public Task<PartnerDto?> UpdatePartnerAsync(Guid partnerId, PartnerUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<PartnerDto>($"/v1/partners/{partnerId}", request, cancellationToken);

    public Task<Product?> CreateProductAsync(ProductCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Product>("/v1/products", request, cancellationToken);

    public Task<Product?> UpdateProductAsync(Guid productId, ProductUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Product>($"/v1/products/{productId}", request, cancellationToken);

    public Task<ProductInventory?> UpdateProductInventoryAsync(Guid productId, ProductInventoryUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<ProductInventory>($"/v1/products/{productId}/inventory", request, cancellationToken);

    public Task<Service?> CreateServiceAsync(ServiceCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Service>("/v1/services", request, cancellationToken);

    public Task<Service?> UpdateServiceAsync(Guid serviceId, ServiceUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Service>($"/v1/services/{serviceId}", request, cancellationToken);

    public Task<Professional?> CreateProfessionalAsync(ProfessionalCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Professional>("/v1/professionals", request, cancellationToken);

    public Task<Professional?> UpdateProfessionalAsync(Guid professionalId, ProfessionalUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Professional>($"/v1/professionals/{professionalId}", request, cancellationToken);

    public Task<PartnerDto?> GetPartnerAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<PartnerDto>($"/v1/partners/{partnerId}", cancellationToken);

    public Task<PartnerActivationStatus?> GetActivationStatusAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<PartnerActivationStatus>($"/v1/partners/{partnerId}/activation", cancellationToken);

    public Task<PartnerDto?> UpdateVisibilityAsync(Guid partnerId, bool isVisible, CancellationToken cancellationToken = default)
        => PatchAsync<PartnerDto>($"/v1/partners/{partnerId}/visibility", new { isVisible }, cancellationToken);

    public Task<MercadoPagoOAuthStart?> StartMercadoPagoOAuthAsync(Guid sellerId, CancellationToken cancellationToken = default)
        => GetAsync<MercadoPagoOAuthStart>($"/api/mercadopago/oauth/start?sellerId={sellerId}", cancellationToken);

    public Task<SellerMercadoPagoStatus?> GetMercadoPagoStatusAsync(Guid sellerId, CancellationToken cancellationToken = default)
        => GetAsync<SellerMercadoPagoStatus>($"/api/sellers/{sellerId}/mercadopago/status", cancellationToken);

    public async Task DisconnectMercadoPagoAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/sellers/{sellerId}/mercadopago/disconnect");
        await SendAsync(request, cancellationToken);
    }

    public Task<SellerFeeSettings?> UpdateSellerFeesAsync(Guid sellerId, UpdateSellerFees request, CancellationToken cancellationToken = default)
        => SendPutAsync<SellerFeeSettings>($"/api/sellers/{sellerId}/fees", request, cancellationToken);

    public Task<IReadOnlyList<MarketplacePaymentDetail>?> GetMarketplacePaymentsAsync(Guid sellerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<MarketplacePaymentDetail>>($"/api/sellers/{sellerId}/payments", cancellationToken);

    public Task<MarketplacePaymentDetail?> GetMarketplacePaymentAsync(Guid paymentId, CancellationToken cancellationToken = default)
        => GetAsync<MarketplacePaymentDetail>($"/api/payments/{paymentId}", cancellationToken);

    public async Task RetryMarketplacePaymentSyncAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/payments/{paymentId}/sync");
        await SendAsync(request, cancellationToken);
    }
}

public sealed record Product(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    string? Name,
    string? Description,
    string? Category,
    string? ImageUrl,
    double Price,
    string? Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ProductInventory? Inventory
);

public sealed record ProductInventory(
    Guid ProductId,
    int Quantity,
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
    string? InternalNote,
    string? OutcomeReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record BookingStatusUpdateRequest(
    string Status
);

public sealed record BookingWorkflowUpdateRequest(
    string? Status,
    DateTimeOffset? StartAt,
    DateTimeOffset? EndAt,
    string? InternalNote,
    string? OutcomeReason
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
    DateTimeOffset CreatedAt,
    string? BatchStatus
);

public sealed record Lead(
    Guid Id,
    Guid TenantId,
    Guid ProfessionalId,
    Guid CustomerId,
    string? Status,
    string? Priority,
    string? Owner,
    DateTimeOffset? NextFollowUpAt,
    string? InternalNote,
    string? OutcomeReason,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record LeadStatusUpdateRequest(
    string Status
);

public sealed record LeadWorkflowUpdateRequest(
    string? Status,
    string? Priority,
    string? Owner,
    DateTimeOffset? NextFollowUpAt,
    string? InternalNote,
    string? OutcomeReason
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
    DateTimeOffset? ReadAt,
    DateTimeOffset? ArchivedAt,
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
    double? Latitude,
    double? Longitude,
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
    Guid? SubcategoryId,
    double? Latitude,
    double? Longitude
);

public sealed record PartnerUpdateRequest(
    string? Type,
    string? Name,
    string? Rut,
    string? Address,
    string? Phone,
    string? Email,
    Guid? SubcategoryId,
    double? Latitude,
    double? Longitude,
    bool? IsVisible
);

public sealed record ProductCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    string? ImageUrl,
    double Price,
    string? Currency,
    bool? IsActive
);

public sealed record ProductUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    string? ImageUrl,
    double? Price,
    string? Currency,
    bool? IsActive
);

public sealed record ProductInventoryUpdateRequest(
    int Quantity
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

public sealed record ServiceUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    double? Price,
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

public sealed record ProfessionalUpdateRequest(
    string? Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? IsVerified,
    bool? IsActive
);
