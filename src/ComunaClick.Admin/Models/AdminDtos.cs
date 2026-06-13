namespace ComunaClick.Admin.Models;

public sealed record AdminProviderBreakdownDto(
    string Provider,
    int Count,
    decimal Amount);

public sealed record AdminDashboardSummaryDto(
    decimal CapturedAmount,
    int CapturedCount,
    int PendingCount,
    int FailedCount,
    int ActiveSubscriptionCandidates,
    IReadOnlyList<AdminProviderBreakdownDto> Providers);

public sealed record AdminProviderEventDto(
    Guid Id,
    string ProviderEventId,
    string EventType,
    string Payload,
    DateTimeOffset ReceivedAt);

public sealed record AdminPaymentIntentListItemDto(
    Guid Id,
    string ExternalReference,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string? ProviderToken,
    string? AuthorizationCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AdminPaymentIntentDetailDto(
    Guid Id,
    string ExternalReference,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string? ProviderToken,
    string? AuthorizationCode,
    string RawResponse,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminProviderEventDto> Events);

public sealed record AdminSubscriptionCandidateDto(
    string ExternalReference,
    string PlanName,
    string Provider,
    string Status,
    decimal Amount,
    string Currency,
    DateTimeOffset LastPaymentAt,
    DateTimeOffset NextBillingAt);

public sealed record AdminDashboardSummaryModel(
    decimal CapturedAmount,
    int CapturedCount,
    int PendingCount,
    int FailedCount,
    int ActiveSubscriptionCandidates,
    IReadOnlyList<AdminProviderBreakdownDto> Providers);

// ── Orders ──────────────────────────────────────────────────────────────────

public sealed record AdminPagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record AdminOrderListItemDto(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    string PartnerName,
    string? BuyerName,
    string? BuyerEmail,
    string Status,
    decimal TotalAmount,
    decimal DeliveryFee,
    string Currency,
    string? DeliveryType,
    string? DeliveryStatus,
    string? PaymentStatus,
    DateTimeOffset CreatedAt);

public sealed record AdminOrderDetailDto(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    string PartnerName,
    string? BuyerName,
    string? BuyerEmail,
    string Status,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal TotalAmount,
    decimal PlatformFeeAmount,
    decimal NetAmount,
    string Currency,
    string? DeliveryType,
    string? DeliveryStatus,
    string? DeliveryAddress,
    string? DeliveryProviderName,
    Guid? CourierId,
    string? CourierName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AdminOrderItemDto> Items,
    AdminOrderPaymentDto? Payment,
    AdminOrderSettlementDto? DeliverySettlement);

public sealed record AdminOrderItemDto(
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

public sealed record AdminOrderPaymentDto(
    Guid Id,
    string Provider,
    string Status,
    string? StatusDetail,
    decimal Amount,
    decimal? PaidAmount,
    string? MercadoPagoPaymentId,
    string? PaymentMethod,
    DateTimeOffset? DateApproved,
    DateTimeOffset CreatedAt);

public sealed record AdminOrderSettlementDto(
    Guid Id,
    string Status,
    decimal GrossAmount,
    decimal PlatformFeeAmount,
    decimal? MercadoPagoFeeAmount,
    decimal NetToCourierAmount,
    DateTimeOffset? SettledAt);

// ── Delivery settlements ─────────────────────────────────────────────────────

public sealed record AdminDeliverySettlementDto(
    Guid Id,
    Guid OrderId,
    Guid PartnerId,
    Guid? CourierId,
    string? CourierName,
    decimal GrossAmount,
    decimal PlatformFeeAmount,
    decimal? MercadoPagoFeeAmount,
    decimal NetToCourierAmount,
    string Currency,
    string Status,
    DateTimeOffset? SettledAt,
    string? Notes,
    DateTimeOffset CreatedAt);

// ── Couriers ─────────────────────────────────────────────────────────────────

public sealed record AdminCourierListItemDto(
    Guid Id,
    Guid TenantId,
    Guid PartnerId,
    string PartnerName,
    string Name,
    string Phone,
    string? Company,
    string? Email,
    string Kind,
    bool IsAvailable,
    bool HasAccount,
    string MercadoPagoStatus,
    int PendingSettlements,
    decimal PendingAmount,
    DateTimeOffset UpdatedAt);

// ── Sellers / Marketplace ────────────────────────────────────────────────────

public sealed record AdminSellerListItemDto(
    Guid Id,
    Guid TenantId,
    string Name,
    string Email,
    bool IsActive,
    string MercadoPagoStatus,
    string? MpUserId,
    DateTimeOffset? ConnectedAt,
    decimal PercentageFee,
    decimal FixedFeeAmount,
    int PaymentCount,
    DateTimeOffset UpdatedAt);

// ── Payouts ──────────────────────────────────────────────────────────────────

public sealed record AdminPayoutBatchListItemDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string Status,
    int ItemCount,
    decimal NetTotal,
    DateTimeOffset CreatedAt);
