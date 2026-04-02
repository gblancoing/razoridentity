using ComunaClick.Common.Funnel;
using ComunaClick.Shared.Http;
using System.Net.Http.Json;

namespace ComunaClick.Shared.Api.Buyer;

public sealed class BuyerApiClient : ApiClientBase
{
    public BuyerApiClient(HttpClient httpClient, Auth.Interfaces.ITokenStore tokenStore, Auth.Interfaces.IAuthClient authClient)
        : base(httpClient, tokenStore, authClient)
    {
    }

    public Task<IReadOnlyList<SearchResultItem>?> SearchAsync(string? query, string? type, int? limit, GeoFilter? geo = null, CancellationToken cancellationToken = default)
    {
        var path = BuildSearchPath("/v1/search", query, type, limit, geo);
        return GetAsync<IReadOnlyList<SearchResultItem>>(path, cancellationToken);
    }

    public Task<IReadOnlyList<SearchResultItem>?> SearchPartnersAsync(string? query, int? limit, GeoFilter? geo = null, CancellationToken cancellationToken = default)
    {
        var path = BuildSearchPath("/v1/search/partners", query, null, limit, geo);
        return GetAsync<IReadOnlyList<SearchResultItem>>(path, cancellationToken);
    }

    public Task<IReadOnlyList<SearchResultItem>?> SearchProfessionalsAsync(string? query, bool? verified, int? limit, GeoFilter? geo = null, CancellationToken cancellationToken = default)
    {
        var path = BuildSearchPath("/v1/search/professionals", query, null, limit, geo) + $"&verified={(verified ?? false)}";
        return GetAsync<IReadOnlyList<SearchResultItem>>(path, cancellationToken);
    }

    public Task<Order?> CreateOrderAsync(OrderCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Order>("/v1/orders", request, cancellationToken);

    public Task<Booking?> CreateBookingAsync(BookingCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Booking>("/v1/bookings", request, cancellationToken);

    public Task<Lead?> CreateLeadAsync(LeadCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Lead>("/v1/leads", request, cancellationToken);

    public Task TrackFunnelEventAsync(FunnelEventRequest request, CancellationToken cancellationToken = default)
        => PostNoContentAsync("/v1/funnel/events", request, cancellationToken);

    public Task<OrderTracking?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<OrderTracking>($"/v1/public/orders/{id}", cancellationToken);

    public Task<BookingTracking?> GetBookingAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<BookingTracking>($"/v1/public/bookings/{id}", cancellationToken);

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Customer>($"/v1/customers/{id}", cancellationToken);

    public Task<Customer?> UpdateCustomerAsync(Guid id, CustomerUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Customer>($"/v1/customers/{id}", request, cancellationToken);

    public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Product>($"/v1/public/products/{id}", cancellationToken);

    public Task<Service?> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Service>($"/v1/public/services/{id}", cancellationToken);

    public Task<Partner?> GetPartnerAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Partner>($"/v1/partners/{id}", cancellationToken);

    public Task<PartnerProfileResponse?> GetPartnerProfileAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<PartnerProfileResponse>($"/v1/public/partners/{id}/profile", cancellationToken);

    public Task<IReadOnlyList<PublicCountryItem>?> GetCountriesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicCountryItem>>("/v1/public/geo/countries", cancellationToken);

    public Task<IReadOnlyList<PublicRegionItem>?> GetRegionsAsync(Guid countryId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicRegionItem>>($"/v1/public/geo/regions?countryId={countryId}", cancellationToken);

    public Task<IReadOnlyList<PublicComunaItem>?> GetComunasAsync(Guid regionId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicComunaItem>>($"/v1/public/geo/comunas?regionId={regionId}", cancellationToken);

    public Task<TenantResolution?> ResolveTenantByComunaAsync(Guid comunaId, CancellationToken cancellationToken = default)
        => PostAsync<TenantResolution>($"/v1/public/geo/tenant-by-comuna/{comunaId}", new { }, cancellationToken);

    public Task<IReadOnlyList<PublicCategoryItem>?> GetProductCategoriesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicCategoryItem>>("/v1/public/catalog/categories", cancellationToken);

    public Task<IReadOnlyList<PublicSubcategoryItem>?> GetProductSubcategoriesAsync(Guid categoryId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicSubcategoryItem>>($"/v1/public/catalog/subcategories?categoryId={categoryId}", cancellationToken);

    public Task<CategoryDiscoveryResponse?> GetCategoryDiscoveryAsync(string categoryCode, CancellationToken cancellationToken = default)
        => GetAsync<CategoryDiscoveryResponse>($"/v1/public/catalog/discovery/{Uri.EscapeDataString(categoryCode)}", cancellationToken);

    public Task<CategoryNearbyResponse?> GetCategoryNearbyAsync(string categoryCode, double latitude, double longitude, int? limit = null, CancellationToken cancellationToken = default)
        => GetAsync<CategoryNearbyResponse>($"/v1/public/catalog/discovery/{Uri.EscapeDataString(categoryCode)}/nearby?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&limit={(limit ?? 24)}", cancellationToken);

    public Task<Professional?> GetProfessionalAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Professional>($"/v1/public/professionals/{id}", cancellationToken);

    public Task<SupportTicketResponse?> CreateSupportTicketAsync(SupportTicketRequest request, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/support/tickets")
        {
            Content = JsonContent.Create(request)
        };

        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<SupportTicketResponse>(message, cancellationToken);
    }

    private static string BuildSearchPath(string basePath, string? query, string? type, int? limit, GeoFilter? geo)
    {
        var parts = new List<string>
        {
            $"query={Uri.EscapeDataString(query ?? string.Empty)}",
            $"limit={(limit ?? 20)}"
        };

        if (!string.IsNullOrWhiteSpace(type))
        {
            parts.Add($"type={Uri.EscapeDataString(type)}");
        }

        if (geo?.CountryId is Guid countryId && countryId != Guid.Empty)
        {
            parts.Add($"countryId={countryId}");
        }

        if (geo?.RegionId is Guid regionId && regionId != Guid.Empty)
        {
            parts.Add($"regionId={regionId}");
        }

        if (geo?.ComunaId is Guid comunaId && comunaId != Guid.Empty)
        {
            parts.Add($"comunaId={comunaId}");
        }

        return $"{basePath}?{string.Join("&", parts)}";
    }
}

public sealed record GeoFilter(
    Guid? CountryId,
    Guid? RegionId,
    Guid? ComunaId
);

public sealed record SearchResultItem(
    string? Type,
    Guid Id,
    string? Name,
    string? Category,
    Guid? PartnerId,
    double? Price,
    string? Currency,
    string? CtaLabel,
    string? CtaHref
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

public sealed record OrderTracking(
    Guid Id,
    string? Status,
    double Subtotal,
    double DeliveryFee,
    double TotalAmount,
    string? Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    OrderTrackingPartner? Partner,
    IReadOnlyList<OrderTrackingItem>? Items
);

public sealed record OrderTrackingPartner(
    Guid Id,
    string? Name,
    string? Address,
    string? Phone,
    string? Email
);

public sealed record OrderTrackingItem(
    Guid Id,
    Guid ProductId,
    string? ProductName,
    int Quantity,
    double UnitPrice,
    double TotalPrice
);

public sealed record OrderItem(
    Guid Id,
    Guid OrderId,
    Guid ProductId,
    int Quantity,
    double UnitPrice,
    double TotalPrice
);

public sealed record OrderItemCreateRequest(
    Guid ProductId,
    int Quantity,
    double UnitPrice
);

public sealed record OrderCreateRequest(
    Guid PartnerId,
    Guid CustomerId,
    double DeliveryFee,
    string? Currency,
    IReadOnlyList<OrderItemCreateRequest>? Items
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

public sealed record BookingTracking(
    Guid Id,
    string? Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    double Amount,
    string? Currency,
    string? CancellationPolicy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    BookingTrackingPartner? Partner,
    BookingTrackingService? Service,
    BookingTrackingProfessional? Professional
);

public sealed record BookingTrackingPartner(
    Guid Id,
    string? Name,
    string? Address,
    string? Phone,
    string? Email
);

public sealed record BookingTrackingService(
    Guid Id,
    string? Name,
    string? Description,
    string? Category,
    int DurationMinutes
);

public sealed record BookingTrackingProfessional(
    Guid Id,
    string? Name,
    string? Specialty,
    string? Email,
    string? Phone
);

public sealed record BookingCreateRequest(
    Guid PartnerId,
    Guid ServiceId,
    Guid? SlotId,
    Guid CustomerId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    double Amount,
    string? Currency,
    string? CancellationPolicy
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

public sealed record LeadCreateRequest(
    Guid ProfessionalId,
    Guid CustomerId,
    string? Message
);

public sealed record Customer(
    Guid Id,
    Guid TenantId,
    string? Email,
    string? Phone,
    string? FullName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CustomerUpdateRequest(
    string? Email,
    string? Phone,
    string? FullName
);

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

public sealed record Partner(
    Guid Id,
    Guid TenantId,
    string? Type,
    string? Name,
    string? Rut,
    string? Address,
    string? Phone,
    string? Email,
    bool IsVisible,
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

public sealed record PublicCountryItem(
    Guid Id,
    string Code,
    string Name
);

public sealed record PublicRegionItem(
    Guid Id,
    Guid CountryId,
    string Code,
    string Name
);

public sealed record PublicComunaItem(
    Guid Id,
    Guid RegionId,
    string Code,
    string Name
);

public sealed record TenantResolution(
    Guid TenantId,
    Guid ComunaId,
    string TenantName,
    string ComunaName,
    string RegionName,
    string CountryName
);

public sealed record PublicCategoryItem(
    Guid Id,
    string Code,
    string Name
);

public sealed record PublicSubcategoryItem(
    Guid Id,
    Guid CategoryId,
    string Code,
    string Name
);

public sealed record CategoryDiscoveryResponse(
    PublicCategoryItem Category,
    IReadOnlyList<PublicSubcategoryItem>? Subcategories,
    IReadOnlyList<CategoryBusinessItem>? Businesses,
    IReadOnlyList<CategoryServiceItem>? Services,
    IReadOnlyList<CategoryProfessionalItem>? Professionals
);

public sealed record CategoryBusinessItem(
    Guid Id,
    string? Type,
    string? Name,
    string? Address,
    string? Phone,
    string? Email,
    string? CategoryName,
    string? SubcategoryName
);

public sealed record CategoryServiceItem(
    Guid Id,
    string? Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    Guid PartnerId,
    string? PartnerName
);

public sealed record CategoryProfessionalItem(
    Guid Id,
    string? Name,
    string? Specialty,
    string? Bio,
    string? Email,
    string? Phone
);

public sealed record CategoryNearbyResponse(
    PublicCategoryItem Category,
    NearbyPoint UserLocation,
    IReadOnlyList<CategoryNearbyBusinessItem>? Businesses
);

public sealed record NearbyPoint(
    double Latitude,
    double Longitude
);

public sealed record CategoryNearbyBusinessItem(
    Guid Id,
    string? Type,
    string? Name,
    string? Address,
    string? Phone,
    string? Email,
    string? CategoryName,
    string? SubcategoryName,
    Guid? ComunaId,
    string? ComunaName,
    double Latitude,
    double Longitude,
    double DistanceKm
);

public sealed record PartnerProfileResponse(
    PartnerProfileSummary Partner,
    IReadOnlyList<PartnerProfileProduct>? Products,
    IReadOnlyList<PartnerProfileService>? Services,
    IReadOnlyList<PartnerProfileProfessional>? Professionals
);

public sealed record PartnerProfileSummary(
    Guid Id,
    string? Type,
    string? Name,
    string? Address,
    string? Phone,
    string? Email,
    string? CategoryName,
    string? SubcategoryName,
    string? OfferLabel
);

public sealed record PartnerProfileProduct(
    Guid Id,
    string? Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency
);

public sealed record PartnerProfileService(
    Guid Id,
    string? Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    int DurationMinutes
);

public sealed record PartnerProfileProfessional(
    Guid Id,
    string? Name,
    string? Specialty,
    string? Bio,
    string? Email,
    string? Phone
);

public sealed class SupportTicketRequest
{
    public Guid? CustomerId { get; set; }
    public Guid? PartnerId { get; set; }
    public string? Email { get; set; }
    public string? Name { get; set; }
    public string? Topic { get; set; }
    public string? Message { get; set; }
}

public sealed record SupportTicketResponse(Guid TicketId);
