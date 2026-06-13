namespace ComunaClick.Api.Modules.Admin;

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
