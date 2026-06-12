using ComunaClick.Common;
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

    public Task<IReadOnlyList<PartnerCourier>?> GetPartnerCouriersAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PartnerCourier>>($"/v1/partners/{partnerId}/couriers", cancellationToken);

    public Task<PartnerCourier?> CreatePartnerCourierAsync(Guid partnerId, PartnerCourierCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<PartnerCourier>($"/v1/partners/{partnerId}/couriers", request, cancellationToken);

    public Task DeletePartnerCourierAsync(Guid partnerId, Guid courierId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/v1/partners/{partnerId}/couriers/{courierId}", cancellationToken);

    /// <summary>Asigna (o reasigna) un repartidor al pedido y devuelve el link seguro para compartirle.</summary>
    public Task<AssignCourierResult?> AssignCourierAsync(Guid orderId, Guid courierId, CancellationToken cancellationToken = default)
        => PostAsync<AssignCourierResult>($"/v1/orders/{orderId}/assign-courier", new { courierId }, cancellationToken);

    /// <summary>Cancela el envío (no la orden): revoca el link del repartidor y notifica al comprador.</summary>
    public Task CancelDeliveryAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PostNoContentAsync($"/v1/orders/{orderId}/cancel-delivery", new { }, cancellationToken);

    public Task<Order?> UpdateOrderStatusAsync(Guid orderId, OrderStatusUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Order>($"/v1/orders/{orderId}/status", request, cancellationToken);

    public Task<Order?> CancelOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => PostAsync<Order>($"/v1/orders/{orderId}/cancel", new { }, cancellationToken);

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

    public Task<IReadOnlyList<ComunaClick.Shared.Api.Buyer.InboxThreadListItem>?> GetPartnerInboxThreadsAsync(
        Guid partnerId,
        string folder = "inbox",
        CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ComunaClick.Shared.Api.Buyer.InboxThreadListItem>>(
            $"/v1/inbox/threads/partner/{partnerId}?folder={Uri.EscapeDataString(folder)}",
            cancellationToken);

    public Task<ComunaClick.Shared.Api.Buyer.InboxThreadDetailResponse?> GetInboxThreadAsync(Guid threadId, CancellationToken cancellationToken = default)
        => GetAsync<ComunaClick.Shared.Api.Buyer.InboxThreadDetailResponse>($"/v1/inbox/threads/{threadId}", cancellationToken);

    public Task<ComunaClick.Shared.Api.Buyer.InboxMessageItem?> ReplyInboxThreadAsync(Guid threadId, string body, CancellationToken cancellationToken = default)
        => PostAsync<ComunaClick.Shared.Api.Buyer.InboxMessageItem>(
            $"/v1/inbox/threads/{threadId}/messages",
            new ComunaClick.Shared.Api.Buyer.InboxMessageCreateRequest(body),
            cancellationToken);

    public Task MarkInboxThreadReadAsync(Guid threadId, CancellationToken cancellationToken = default)
        => SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/v1/inbox/threads/{threadId}/read"), cancellationToken);

    public Task UpdateInboxThreadStatusAsync(Guid threadId, string status, CancellationToken cancellationToken = default)
        => PatchAsync<object>($"/v1/inbox/threads/{threadId}/status", new ComunaClick.Shared.Api.Buyer.InboxThreadStatusRequest(status), cancellationToken);

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

    public Task<PartnerDto?> UpdatePartnerBankAccountAsync(
        Guid partnerId,
        PartnerBankAccountUpdateRequest request,
        CancellationToken cancellationToken = default)
        => PatchAsync<PartnerDto>($"/v1/partners/{partnerId}/bank-account", request, cancellationToken);

    public Task<PartnerDto?> UpdatePartnerWebLinksAsync(
        Guid partnerId,
        ProfileWebLinksUpdateRequest request,
        CancellationToken cancellationToken = default)
        => PatchAsync<PartnerDto>($"/v1/partners/{partnerId}/web-links", request, cancellationToken);

    public Task<Product?> CreateProductAsync(ProductCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Product>("/v1/products", request, cancellationToken);

    public Task<Product?> UpdateProductAsync(Guid productId, ProductUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Product>($"/v1/products/{productId}", request, cancellationToken);

    public Task<ProductInventory?> UpdateProductInventoryAsync(Guid productId, ProductInventoryUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<ProductInventory>($"/v1/products/{productId}/inventory", request, cancellationToken);

    public Task DeleteProductAsync(Guid productId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/v1/products/{productId}", cancellationToken);

    public Task<IReadOnlyList<PartnerCatalogCategoryInfo>?> GetPartnerCatalogCategoriesAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PartnerCatalogCategoryInfo>>($"/v1/partners/{partnerId}/catalog-categories", cancellationToken);

    public Task<PartnerCatalogCategoryInfo?> CreatePartnerCatalogCategoryAsync(
        Guid partnerId,
        PartnerCatalogCategoryCreateRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync<PartnerCatalogCategoryInfo>($"/v1/partners/{partnerId}/catalog-categories", request, cancellationToken);

    public Task<PartnerCatalogCategoryInfo?> UpdatePartnerCatalogCategoryAsync(
        Guid partnerId,
        Guid categoryId,
        PartnerCatalogCategoryUpdateRequest request,
        CancellationToken cancellationToken = default)
        => PatchAsync<PartnerCatalogCategoryInfo>($"/v1/partners/{partnerId}/catalog-categories/{categoryId}", request, cancellationToken);

    public Task DeletePartnerCatalogCategoryAsync(Guid partnerId, Guid categoryId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/v1/partners/{partnerId}/catalog-categories/{categoryId}", cancellationToken);

    public async Task<IReadOnlyList<ProductImageInfo>?> UploadProductImagesAsync(
        Guid productId,
        IReadOnlyList<ProductImageUploadFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return Array.Empty<ProductImageInfo>();
        }

        var multipart = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.Content);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            multipart.Add(streamContent, "files", file.FileName);
        }

        var message = new HttpRequestMessage(HttpMethod.Post, $"/v1/products/{productId}/images")
        {
            Content = multipart
        };

        return await SendAsync<IReadOnlyList<ProductImageInfo>>(message, cancellationToken);
    }

    public Task DeleteProductImageAsync(Guid productId, Guid imageId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/v1/products/{productId}/images/{imageId}", cancellationToken);

    public Task<Service?> CreateServiceAsync(ServiceCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Service>("/v1/services", request, cancellationToken);

    public Task<Service?> GetServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
        => GetAsync<Service>($"/v1/services/{serviceId}", cancellationToken);

    public Task<Service?> UpdateServiceAsync(Guid serviceId, ServiceUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Service>($"/v1/services/{serviceId}", request, cancellationToken);

    public Task<Service?> UpdateServiceGeoAsync(
        Guid serviceId,
        Guid? countryId,
        Guid? regionId,
        Guid? comunaId,
        double latitude,
        double longitude,
        CancellationToken cancellationToken = default)
        => PatchAsync<Service>($"/v1/services/{serviceId}/geo", new ServiceGeoPatchRequest(
            countryId,
            regionId,
            comunaId,
            latitude,
            longitude), cancellationToken);

    public Task DeleteServiceAsync(Guid serviceId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/v1/services/{serviceId}", cancellationToken);

    public async Task<IReadOnlyList<ServiceImageInfo>?> UploadServiceImagesAsync(
        Guid serviceId,
        IReadOnlyList<ServiceImageUploadFile> files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count == 0)
        {
            return Array.Empty<ServiceImageInfo>();
        }

        var multipart = new MultipartFormDataContent();
        foreach (var file in files)
        {
            var streamContent = new StreamContent(file.Content);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
            multipart.Add(streamContent, "files", file.FileName);
        }

        var message = new HttpRequestMessage(HttpMethod.Post, $"/v1/services/{serviceId}/images")
        {
            Content = multipart
        };

        return await SendAsync<IReadOnlyList<ServiceImageInfo>>(message, cancellationToken);
    }

    public Task DeleteServiceImageAsync(Guid serviceId, Guid imageId, CancellationToken cancellationToken = default)
        => DeleteAsync($"/v1/services/{serviceId}/images/{imageId}", cancellationToken);

    public Task<Professional?> CreateProfessionalAsync(ProfessionalCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Professional>("/v1/professionals", request, cancellationToken);

    public Task<Professional?> UpdateProfessionalAsync(Guid professionalId, ProfessionalUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Professional>($"/v1/professionals/{professionalId}", request, cancellationToken);

    public Task<Professional?> UpdateProfessionalWebLinksAsync(
        Guid professionalId,
        ProfileWebLinksUpdateRequest request,
        CancellationToken cancellationToken = default)
        => PatchAsync<Professional>($"/v1/professionals/{professionalId}/web-links", request, cancellationToken);

    public Task<PartnerDto?> GetPartnerAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<PartnerDto>($"/v1/partners/{partnerId}", cancellationToken);

    public Task<PartnerStorefrontSettings?> GetPartnerStorefrontAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<PartnerStorefrontSettings>($"/v1/partners/{partnerId}/storefront", cancellationToken);

    public Task<PartnerStorefrontSettings?> UpdatePartnerStorefrontAsync(
        Guid partnerId,
        PartnerStorefrontUpdateRequest request,
        CancellationToken cancellationToken = default)
        => PatchAsync<PartnerStorefrontSettings>($"/v1/partners/{partnerId}/storefront", request, cancellationToken);

    public async Task<PartnerStorefrontSettings?> UploadPartnerBannerAsync(
        Guid partnerId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var multipart = new MultipartFormDataContent();
        multipart.Add(streamContent, "file", fileName);
        var message = new HttpRequestMessage(HttpMethod.Post, $"/v1/partners/{partnerId}/storefront/banner")
        {
            Content = multipart
        };
        return await SendAsync<PartnerStorefrontSettings>(message, cancellationToken);
    }

    public async Task<PartnerStorefrontSettings?> UploadPartnerLogoAsync(
        Guid partnerId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var multipart = new MultipartFormDataContent();
        multipart.Add(streamContent, "file", fileName);
        var message = new HttpRequestMessage(HttpMethod.Post, $"/v1/partners/{partnerId}/storefront/logo")
        {
            Content = multipart
        };
        return await SendAsync<PartnerStorefrontSettings>(message, cancellationToken);
    }

    public Task<ProfessionalStorefrontSettings?> GetProfessionalStorefrontAsync(
        Guid professionalId,
        CancellationToken cancellationToken = default)
        => GetAsync<ProfessionalStorefrontSettings>($"/v1/professionals/{professionalId}/storefront", cancellationToken);

    public Task<ProfessionalStorefrontSettings?> UpdateProfessionalStorefrontAsync(
        Guid professionalId,
        ProfessionalStorefrontUpdateRequest request,
        CancellationToken cancellationToken = default)
        => PatchAsync<ProfessionalStorefrontSettings>($"/v1/professionals/{professionalId}/storefront", request, cancellationToken);

    public async Task<ProfessionalStorefrontSettings?> UploadProfessionalBannerAsync(
        Guid professionalId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var multipart = new MultipartFormDataContent();
        multipart.Add(streamContent, "file", fileName);
        var message = new HttpRequestMessage(HttpMethod.Post, $"/v1/professionals/{professionalId}/storefront/banner")
        {
            Content = multipart
        };
        return await SendAsync<ProfessionalStorefrontSettings>(message, cancellationToken);
    }

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
    Guid? PartnerCatalogCategoryId,
    string? CatalogCategoryName,
    string? ImageUrl,
    double Price,
    double? CostPrice,
    string? Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    ProductInventory? Inventory,
    IReadOnlyList<string>? ImageUrls = null,
    IReadOnlyList<ProductImageInfo>? Images = null,
    bool? InStock = null,
    int? StockQuantity = null,
    int? AvailableQuantity = null,
    int? ReservedQuantity = null,
    string? ProductAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<Guid>? DiscoverySubcategoryIds = null
);

public sealed record ProductImageInfo(Guid Id, string Url, int SortOrder);

public sealed record ProductImageUploadFile(Stream Content, string FileName, string ContentType);

public sealed record PartnerCatalogCategoryInfo(
    Guid Id,
    Guid PartnerId,
    string Name,
    Guid? ParentId,
    int SortOrder,
    bool IsActive);

public sealed record PartnerCatalogCategoryCreateRequest(string Name, int? SortOrder = null, Guid? ParentId = null);

public sealed record PartnerCatalogCategoryUpdateRequest(
    string? Name = null,
    int? SortOrder = null,
    bool? IsActive = null,
    Guid? ParentId = null,
    bool? ClearParent = null);

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
    bool IsBookable,
    bool RequiresOnlinePayment,
    string? ImageUrl,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Guid>? ProfessionalIds = null,
    string? ServiceAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<string>? ImageUrls = null,
    IReadOnlyList<ServiceImageInfo>? Images = null
);

public sealed record ServiceImageInfo(Guid Id, string Url, int SortOrder);

public sealed record ServiceImageUploadFile(Stream Content, string FileName, string ContentType);

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
    IReadOnlyList<OrderItem>? Items,
    double GrossAmount = 0,
    double PlatformFeeAmount = 0,
    double NetAmount = 0,
    string? BuyerName = null,
    string? BuyerPhone = null,
    double? MercadoPagoFeeAmount = null,
    string? DeliveryAddress = null,
    string? DeliveryType = null,
    string? DeliveryStatus = null,
    Guid? CourierId = null
);

public sealed record PartnerCourier(
    Guid Id,
    Guid PartnerId,
    string Name,
    string Phone,
    string? Company,
    bool IsAvailable);

public sealed record PartnerCourierCreateRequest(string Name, string Phone, string? Company);

public sealed record AssignCourierResult(
    Guid OrderId,
    Guid CourierId,
    string CourierName,
    string CourierPhone,
    string CourierLink,
    string DeliveryStatus);

public sealed record OrderItem(
    Guid Id,
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    double UnitPrice,
    double TotalPrice
);

public sealed record OrderStatusUpdateRequest(string Status);

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
    Guid? PartnerId,
    string? Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool IsVerified,
    bool IsActive,
    DateTimeOffset CreatedAt,
    string? WebsiteUrl = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null,
    string? LinkedInUrl = null,
    string? XUrl = null,
    string? TikTokUrl = null,
    string? YouTubeUrl = null,
    string? OtherLinkLabel = null,
    string? OtherLinkUrl = null
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
    Guid? CountryId,
    Guid? RegionId,
    Guid? ComunaId,
    double? Latitude,
    double? Longitude,
    bool IsVisible,
    bool OffersServices,
    string? BankName,
    string? BankAccountType,
    string? BankAccountNumber,
    string? BankAccountHolder,
    string? BankAccountHolderRut,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    PartnerActivationStatus? Activation,
    string? WebsiteUrl = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null,
    string? LinkedInUrl = null,
    string? XUrl = null,
    string? TikTokUrl = null,
    string? YouTubeUrl = null,
    string? OtherLinkLabel = null,
    string? OtherLinkUrl = null
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
    Guid? CountryId,
    Guid? RegionId,
    Guid? ComunaId,
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
    bool? IsVisible,
    bool? OffersServices,
    string? BankName = null,
    string? BankAccountType = null,
    string? BankAccountNumber = null,
    string? BankAccountHolder = null,
    string? BankAccountHolderRut = null
);

public sealed record PartnerBankAccountUpdateRequest(
    string BankName,
    string BankAccountType,
    string BankAccountNumber,
    string BankAccountHolder,
    string? BankAccountHolderRut
);

public sealed record ProductCreateRequest(
    Guid PartnerId,
    string Name,
    string? Description,
    string? Category,
    Guid? PartnerCatalogCategoryId,
    string? ImageUrl,
    double Price,
    double? CostPrice,
    string? Currency,
    bool? IsActive,
    int? InitialStock,
    string? ProductAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<Guid>? DiscoverySubcategoryIds = null
);

public sealed record ProductUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    Guid? PartnerCatalogCategoryId,
    string? ImageUrl,
    double? Price,
    double? CostPrice,
    string? Currency,
    bool? IsActive,
    string? ProductAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<Guid>? DiscoverySubcategoryIds = null
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
    bool? IsActive,
    string? ImageUrl = null,
    string? ServiceAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<Guid>? ProfessionalIds = null
);

public sealed record ServiceUpdateRequest(
    string? Name,
    string? Description,
    string? Category,
    double? Price,
    string? Currency,
    int? DurationMinutes,
    bool? IsActive,
    string? ImageUrl = null,
    string? ServiceAddress = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    IReadOnlyList<Guid>? ProfessionalIds = null
);

public sealed record ServiceGeoPatchRequest(
    Guid? CountryId,
    Guid? RegionId,
    Guid? ComunaId,
    double Latitude,
    double Longitude
);

public sealed record ProfessionalCreateRequest(
    string Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? IsVerified,
    bool? IsActive,
    Guid? PartnerId = null
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

public sealed record PartnerStorefrontSettings(
    Guid PartnerId,
    string? BannerUrl,
    string? LogoUrl,
    string? StorefrontTagline,
    string? StorefrontAbout,
    string? StorefrontHighlight1,
    string? StorefrontHighlight2,
    string? StorefrontHighlight3,
    string? PublicProfilePath);

public sealed record PartnerStorefrontUpdateRequest(
    string? StorefrontTagline,
    string? StorefrontAbout,
    string? StorefrontHighlight1,
    string? StorefrontHighlight2,
    string? StorefrontHighlight3,
    bool? RemoveBanner,
    bool? RemoveLogo);

public sealed record ProfessionalStorefrontSettings(
    Guid ProfessionalId,
    string? Name,
    string? Specialty,
    string? BannerUrl,
    string? ProfileHeadline,
    string? Bio,
    string? PublicProfilePath);

public sealed record ProfessionalStorefrontUpdateRequest(
    string? ProfileHeadline,
    string? Bio,
    bool? RemoveBanner);

public sealed record ProfileWebLinksUpdateRequest(
    string? WebsiteUrl,
    string? InstagramUrl,
    string? FacebookUrl,
    string? LinkedInUrl,
    string? XUrl,
    string? TikTokUrl,
    string? YouTubeUrl,
    string? OtherLinkLabel,
    string? OtherLinkUrl);
