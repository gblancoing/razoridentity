using ComunaClick.Common.Funnel;
using ComunaClick.Shared.Http;
using System.Net.Http.Headers;
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
        var path = BuildSearchPath("/v1/search/professionals", query, null, limit, geo);
        if (verified.HasValue)
        {
            path += $"&verified={verified.Value.ToString().ToLowerInvariant()}";
        }
        return GetAsync<IReadOnlyList<SearchResultItem>>(path, cancellationToken);
    }

    public Task<Order?> CreateOrderAsync(OrderCreateRequest request, CancellationToken cancellationToken = default)
        => CreateOrderAsync(request, null, cancellationToken);

    public Task<Order?> CreateOrderAsync(OrderCreateRequest request, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/orders")
        {
            Content = JsonContent.Create(request)
        };

        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<Order>(message, cancellationToken);
    }

    public Task<GuestOrderCreateResponse?> CreateGuestOrderAsync(
        GuestOrderCreateRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync<GuestOrderCreateResponse>("/v1/public/orders/guest", request, cancellationToken);

    public Task<GuestBookingCreateResponse?> CreateGuestBookingAsync(
        GuestBookingCreateRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync<GuestBookingCreateResponse>("/v1/public/bookings/guest", request, cancellationToken);

    public Task<PublicPartnerPaymentStatusResponse?> GetPartnerPaymentStatusAsync(
        Guid partnerId,
        CancellationToken cancellationToken = default)
        => GetAsync<PublicPartnerPaymentStatusResponse>($"/v1/public/checkout/partners/{partnerId}/payment-status", cancellationToken);

    public Task<PublicMercadoPagoCheckoutResponse?> CreateMercadoPagoCheckoutAsync(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync<PublicMercadoPagoCheckoutResponse>("/v1/public/checkout/mercadopago", request, cancellationToken);

    public Task<PublicMercadoPagoCheckoutResponse?> ResumeMercadoPagoCheckoutAsync(
        PublicMercadoPagoCheckoutRequest request,
        CancellationToken cancellationToken = default)
        => PostAsync<PublicMercadoPagoCheckoutResponse>("/v1/public/checkout/mercadopago/resume", request, cancellationToken);

    public Task SendPaymentReminderAsync(
        PublicPaymentReminderRequest request,
        CancellationToken cancellationToken = default)
        => PostNoContentAsync("/v1/public/checkout/payment-reminder", request, cancellationToken);

    public Task<IReadOnlyList<PublicServiceSlot>?> GetPublicServiceSlotsAsync(
        Guid serviceId,
        int days = 14,
        CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicServiceSlot>>($"/v1/public/services/{serviceId}/slots?days={days}", cancellationToken);

    public Task<Customer?> LinkGuestCustomerAsync(
        Guid customerId,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
        => PostAsync<Customer>("/v1/buyer/customer/link-guest", new LinkGuestCustomerRequest(customerId, tenantId), cancellationToken);

    public Task<CartSnapshot?> GetCartAsync(CancellationToken cancellationToken = default)
        => GetAsync<CartSnapshot>("/v1/cart", cancellationToken);

    public Task<CartInfo?> UpsertCartItemAsync(CartItemUpsertRequest request, CancellationToken cancellationToken = default)
        => PostAsync<CartInfo>("/v1/cart/items", request, cancellationToken);

    public Task RemoveCartItemAsync(Guid itemId, CancellationToken cancellationToken = default)
        => SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/cart/items/{itemId}"), cancellationToken);

    public Task<Order?> CheckoutCartAsync(CartCheckoutRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Order>("/v1/cart/checkout", request, cancellationToken);

    public Task<IReadOnlyList<DeliveryProviderOption>?> GetDeliveryProvidersAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<DeliveryProviderOption>>($"/v1/delivery/providers?partnerId={partnerId}", cancellationToken);

    public Task<Booking?> CreateBookingAsync(BookingCreateRequest request, CancellationToken cancellationToken = default)
        => CreateBookingAsync(request, null, cancellationToken);

    public Task<Booking?> CreateBookingAsync(BookingCreateRequest request, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/bookings")
        {
            Content = JsonContent.Create(request)
        };

        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<Booking>(message, cancellationToken);
    }

    public Task<Lead?> CreateLeadAsync(LeadCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Lead>("/v1/leads", request, cancellationToken);

    public Task<IReadOnlyList<InboxThreadListItem>?> GetMyInboxThreadsAsync(string folder = "inbox", CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<InboxThreadListItem>>($"/v1/inbox/threads/mine?folder={Uri.EscapeDataString(folder)}", cancellationToken);

    public Task<IReadOnlyList<InboxThreadListItem>?> GetProfessionalInboxThreadsAsync(string folder = "inbox", CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<InboxThreadListItem>>($"/v1/inbox/threads/as-professional?folder={Uri.EscapeDataString(folder)}", cancellationToken);

    public Task<InboxThreadDetailResponse?> GetInboxThreadAsync(Guid threadId, CancellationToken cancellationToken = default)
        => GetAsync<InboxThreadDetailResponse>($"/v1/inbox/threads/{threadId}", cancellationToken);

    public Task<InboxThreadCreateResult?> CreateInboxThreadAsync(InboxThreadCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<InboxThreadCreateResult>("/v1/inbox/threads", request, cancellationToken);

    public Task<InboxThreadCreateResult?> CreateGuestInboxThreadAsync(GuestInboxThreadCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<InboxThreadCreateResult>("/v1/public/inbox/threads", request, cancellationToken);

    public Task<InboxMessageItem?> ReplyInboxThreadAsync(Guid threadId, string body, CancellationToken cancellationToken = default)
        => PostAsync<InboxMessageItem>($"/v1/inbox/threads/{threadId}/messages", new InboxMessageCreateRequest(body), cancellationToken);

    public Task MarkInboxThreadReadAsync(Guid threadId, CancellationToken cancellationToken = default)
        => SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/v1/inbox/threads/{threadId}/read"), cancellationToken);

    public Task UpdateInboxThreadStatusAsync(Guid threadId, string status, CancellationToken cancellationToken = default)
        => PatchAsync<object>($"/v1/inbox/threads/{threadId}/status", new InboxThreadStatusRequest(status), cancellationToken);

    public Task<Customer?> EnsureBuyerCustomerAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/buyer/customer/ensure")
        {
            Content = JsonContent.Create(new BuyerCustomerEnsureRequest(tenantId))
        };

        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<Customer>(message, cancellationToken);
    }

    public Task<IReadOnlyList<BuyerFavoriteItem>?> GetFavoritesAsync(string? type = null, CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/favorites";
        if (!string.IsNullOrWhiteSpace(type))
        {
            path += $"?type={Uri.EscapeDataString(type)}";
        }

        return GetAsync<IReadOnlyList<BuyerFavoriteItem>>(path, cancellationToken);
    }

    public Task<BuyerFavoriteItem?> CreateFavoriteAsync(BuyerFavoriteCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<BuyerFavoriteItem>("/v1/buyer/favorites", request, cancellationToken);

    public Task DeleteFavoriteAsync(Guid favoriteId, CancellationToken cancellationToken = default)
        => SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/buyer/favorites/{favoriteId}"), cancellationToken);

    public Task<Customer?> GetBuyerCustomerAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/customer";
        if (tenantId is not null && tenantId != Guid.Empty)
        {
            path += $"?tenantId={tenantId.Value}";
        }

        var message = new HttpRequestMessage(HttpMethod.Get, path);
        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<Customer>(message, cancellationToken);
    }

    public Task<Customer?> UpdateBuyerCustomerAsync(CustomerUpdateRequest request, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/customer";
        if (tenantId is not null && tenantId != Guid.Empty)
        {
            path += $"?tenantId={tenantId.Value}";
        }

        var message = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = JsonContent.Create(request)
        };

        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<Customer>(message, cancellationToken);
    }

    public Task<Customer?> UploadBuyerCustomerAvatarAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/customer/avatar";
        if (tenantId is not null && tenantId != Guid.Empty)
        {
            path += $"?tenantId={tenantId.Value}";
        }

        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var multipart = new MultipartFormDataContent();
        multipart.Add(streamContent, "file", fileName);

        var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = multipart
        };

        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<Customer>(message, cancellationToken);
    }

    public Task<BuyerProfessionalProfile?> GetBuyerProfessionalProfileAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/professional-profile";
        if (tenantId is not null && tenantId != Guid.Empty)
        {
            path += $"?tenantId={tenantId.Value}";
        }

        var message = new HttpRequestMessage(HttpMethod.Get, path);
        if (tenantId is not null && tenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }

        return SendAsync<BuyerProfessionalProfile>(message, cancellationToken);
    }

    public Task<BuyerProfessionalProfile?> UpsertBuyerProfessionalProfileAsync(
        BuyerProfessionalProfileUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, "/v1/buyer/professional-profile")
        {
            Content = JsonContent.Create(request)
        };

        if (request.TenantId is not null && request.TenantId != Guid.Empty)
        {
            message.Headers.Add("X-Tenant-Id", request.TenantId.Value.ToString());
        }

        return SendAsync<BuyerProfessionalProfile>(message, cancellationToken);
    }

    public async Task<BuyerProfessionalProfile?> UploadBuyerProfessionalBannerAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var multipart = new MultipartFormDataContent();
        multipart.Add(streamContent, "file", fileName);
        var path = "/v1/buyer/professional-profile/banner";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = multipart };
        if (tenantId is not null && tenantId != Guid.Empty)
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        return await SendAsync<BuyerProfessionalProfile>(message, cancellationToken);
    }

    public Task<BuyerProfessionalProfile?> RemoveBuyerProfessionalBannerAsync(
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/professional-profile/banner";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        var message = new HttpRequestMessage(HttpMethod.Delete, path);
        if (tenantId is not null && tenantId != Guid.Empty)
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        return SendAsync<BuyerProfessionalProfile>(message, cancellationToken);
    }

    public async Task<BuyerProfessionalProfile?> AddBuyerProfessionalCertificationAsync(
        BuyerAddCertificationRequest request,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/professional-profile/certifications";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(request) };
        if (tenantId is not null && tenantId != Guid.Empty)
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        return await SendAsync<BuyerProfessionalProfile>(message, cancellationToken);
    }

    public async Task<BuyerProfessionalProfile?> RemoveBuyerProfessionalCertificationAsync(
        string certId,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/v1/buyer/professional-profile/certifications/{certId}";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        var message = new HttpRequestMessage(HttpMethod.Delete, path);
        if (tenantId is not null && tenantId != Guid.Empty)
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        return await SendAsync<BuyerProfessionalProfile>(message, cancellationToken);
    }

    public async Task<BuyerProfessionalProfile?> UploadBuyerProfessionalPhotoAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        var multipart = new MultipartFormDataContent();
        multipart.Add(streamContent, "file", fileName);
        var path = "/v1/buyer/professional-profile/photo";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = multipart };
        if (tenantId is not null && tenantId != Guid.Empty)
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        return await SendAsync<BuyerProfessionalProfile>(message, cancellationToken);
    }

    public Task<IReadOnlyList<ProfessionalFollowItem>?> GetProfessionalFollowsAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/professional-profile/follows";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        return GetAsync<IReadOnlyList<ProfessionalFollowItem>>(path, cancellationToken);
    }

    public async Task<bool> FollowProfessionalEntityAsync(string followedType, Guid followedId, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var path = "/v1/buyer/professional-profile/follows";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { followedType, followedId })
        };
        if (tenantId is not null && tenantId != Guid.Empty)
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        var result = await SendAsync<FollowStatusResponse>(message, cancellationToken);
        return result?.IsFollowing ?? false;
    }

    public async Task<bool> UnfollowProfessionalEntityAsync(string followedType, Guid followedId, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var path = $"/v1/buyer/professional-profile/follows/{followedType}/{followedId}";
        if (tenantId is not null && tenantId != Guid.Empty)
            path += $"?tenantId={tenantId.Value}";
        var message = new HttpRequestMessage(HttpMethod.Delete, path);
        if (tenantId is not null && tenantId != Guid.Empty)
            message.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        var result = await SendAsync<FollowStatusResponse>(message, cancellationToken);
        return result?.IsFollowing ?? false;
    }

    public Task TrackFunnelEventAsync(FunnelEventRequest request, CancellationToken cancellationToken = default)
        => PostNoContentAsync("/v1/funnel/events", request, cancellationToken);

    public Task<OrderTracking?> GetOrderAsync(
        Guid id,
        Guid? customerId = null,
        string? trackingToken = null,
        CancellationToken cancellationToken = default)
    {
        // Acceso preferente por token firmado; el customerId queda solo como respaldo legacy.
        var query = !string.IsNullOrWhiteSpace(trackingToken)
            ? $"token={Uri.EscapeDataString(trackingToken)}"
            : $"customerId={customerId}";
        return GetAsync<OrderTracking>($"/v1/public/orders/{id}?{query}", cancellationToken);
    }

    public Task<BookingTracking?> GetBookingAsync(Guid id, Guid customerId, CancellationToken cancellationToken = default)
        => GetAsync<BookingTracking>($"/v1/public/bookings/{id}?customerId={customerId}", cancellationToken);

    public Task<IReadOnlyList<BookingTracking>?> GetRecentBookingsAsync(int limit = 8, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<BookingTracking>>($"/v1/buyer/bookings/recent?limit={Math.Clamp(limit, 1, 30)}", cancellationToken);

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
        => GetCatalogCategoriesAsync("commerce", cancellationToken);

    public Task<IReadOnlyList<PublicCategoryItem>?> GetServiceCategoriesAsync(CancellationToken cancellationToken = default)
        => GetCatalogCategoriesAsync("service", cancellationToken);

    public Task<IReadOnlyList<PublicCategoryItem>?> GetCatalogCategoriesAsync(string scope, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicCategoryItem>>($"/v1/public/catalog/categories?scope={Uri.EscapeDataString(scope)}", cancellationToken);

    public Task<PublicSiteContentResponse?> GetSiteContentAsync(CancellationToken cancellationToken = default)
        => GetAsync<PublicSiteContentResponse>("/v1/public/site-content", cancellationToken);

    public Task<IReadOnlyList<PublicSubcategoryItem>?> GetProductSubcategoriesAsync(Guid categoryId, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<PublicSubcategoryItem>>($"/v1/public/catalog/subcategories?categoryId={categoryId}", cancellationToken);

    public Task<CategoryDiscoveryResponse?> GetCategoryDiscoveryAsync(string categoryCode, CancellationToken cancellationToken = default)
        => GetAsync<CategoryDiscoveryResponse>($"/v1/public/catalog/discovery/{Uri.EscapeDataString(categoryCode)}", cancellationToken);

    public Task<CategoryNearbyResponse?> GetCategoryNearbyAsync(
        string categoryCode,
        double latitude,
        double longitude,
        int? limit = null,
        string? subcode = null,
        double? maxDistanceKm = null,
        CancellationToken cancellationToken = default)
    {
        var path = $"/v1/public/catalog/discovery/{Uri.EscapeDataString(categoryCode)}/nearby?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&limit={(limit ?? 24)}";
        if (!string.IsNullOrWhiteSpace(subcode))
        {
            path += $"&subcode={Uri.EscapeDataString(subcode)}";
        }
        if (maxDistanceKm is > 0)
        {
            path += $"&maxDistanceKm={maxDistanceKm.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        }
        return GetAsync<CategoryNearbyResponse>(path, cancellationToken);
    }

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

        if (geo?.HasCoordinates == true)
        {
            parts.Add($"latitude={geo.Latitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            parts.Add($"longitude={geo.Longitude!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            var radius = geo.RadiusKm ?? 30;
            parts.Add($"radiusKm={radius.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }

        return $"{basePath}?{string.Join("&", parts)}";
    }
}

public sealed record GeoFilter(
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null)
{
    public bool HasCoordinates =>
        Latitude is double lat && Longitude is double lng
        && lat is >= -90 and <= 90
        && lng is >= -180 and <= 180;
}

public sealed record SearchResultItem(
    string? Type,
    Guid Id,
    string? Name,
    string? Category,
    Guid? PartnerId,
    double? Price,
    string? Currency,
    string? CtaLabel,
    string? CtaHref,
    double? DistanceKm = null,
    double? Latitude = null,
    double? Longitude = null,
    string? LogoUrl = null,
    string? ImageUrl = null,
    IReadOnlyList<string>? ImageUrls = null);

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
    Guid? DeliveryProviderId,
    string? DeliveryProviderName,
    string? DeliveryAddress,
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
    Guid? DeliveryProviderId,
    string? DeliveryProviderName,
    string? DeliveryAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    OrderTrackingPartner? Partner,
    IReadOnlyList<OrderTrackingItem>? Items,
    string? TrackingToken = null
);

public sealed record OrderTrackingPartner(
    Guid Id,
    string? Name,
    string? Address,
    string? Phone
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
    IReadOnlyList<OrderItemCreateRequest>? Items,
    Guid? DeliveryProviderId = null,
    string? DeliveryAddress = null
);

public sealed record GuestContactRequest(
    string FullName,
    string Email,
    string? Phone);

public sealed record GuestOrderCreateRequest(
    Guid TenantId,
    Guid PartnerId,
    IReadOnlyList<OrderItemCreateRequest> Items,
    GuestContactRequest Guest,
    double DeliveryFee = 0,
    string? Currency = null,
    string? DeliveryAddress = null);

public sealed record GuestOrderCreateResponse(
    Guid OrderId,
    Guid CustomerId,
    string? Status,
    double TotalAmount,
    string? Currency,
    string? TrackingToken = null);

public sealed record GuestBookingCreateRequest(
    Guid TenantId,
    Guid ServiceId,
    Guid? SlotId,
    GuestContactRequest Guest,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    string? Currency = null,
    string? CancellationPolicy = null);

public sealed record GuestBookingCreateResponse(
    Guid BookingId,
    Guid CustomerId,
    string? Status,
    double Amount,
    string? Currency);

public sealed record PublicPartnerPaymentStatusResponse(
    Guid PartnerId,
    bool MercadoPagoReady);

public sealed record PublicMercadoPagoCheckoutRequest(
    Guid CustomerId,
    Guid? OrderId,
    Guid? BookingId);

public sealed record PublicMercadoPagoCheckoutResponse(
    bool Available,
    string? CheckoutUrl,
    Guid? PaymentId,
    string? Message);

public sealed record PublicPaymentReminderRequest(
    Guid CustomerId,
    Guid? OrderId,
    Guid? BookingId);

public sealed record LinkGuestCustomerRequest(
    Guid CustomerId,
    Guid? TenantId);

public sealed record PublicServiceSlot(
    Guid Id,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int Capacity);

public sealed record CartItemUpsertRequest(
    Guid ProductId,
    int Quantity
);

public sealed record CartCheckoutRequest(
    Guid CartId,
    Guid? DeliveryProviderId,
    string? DeliveryAddress,
    double? DeliveryFee,
    string? Currency
);

public sealed record CartSnapshot(
    Guid CustomerId,
    IReadOnlyList<CartInfo>? Carts
);

public sealed record CartInfo(
    Guid Id,
    Guid PartnerId,
    string? Status,
    double Subtotal,
    double DeliveryFee,
    double TotalAmount,
    string? Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<CartItem>? Items
);

public sealed record CartItem(
    Guid Id,
    Guid ProductId,
    string? ProductName,
    int Quantity,
    double UnitPrice,
    double TotalPrice
);

public sealed record DeliveryProviderOption(
    Guid Id,
    string Name,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    double BaseFee,
    int? EstimatedMinutes,
    Guid? RegionId,
    Guid? ComunaId
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
    string? Phone
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

public sealed record BuyerCustomerEnsureRequest(
    Guid? TenantId
);

public sealed record Customer(
    Guid Id,
    Guid TenantId,
    string? Email,
    string? Phone,
    string? FullName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? AvatarUrl = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    string? Address = null,
    double? Latitude = null,
    double? Longitude = null
);

public sealed record CustomerUpdateRequest(
    string? Email,
    string? Phone,
    string? FullName,
    string? AvatarUrl = null,
    Guid? CountryId = null,
    Guid? RegionId = null,
    Guid? ComunaId = null,
    string? Address = null,
    double? Latitude = null,
    double? Longitude = null,
    bool UpdateDeliveryAddress = false
);

public sealed record BuyerProfessionalProfile(
    bool HasProfile,
    Guid? ProfessionalId,
    string? Name,
    string? Email,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool IsVerified,
    bool IsActive,
    string? WebsiteUrl = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null,
    string? LinkedInUrl = null,
    string? XUrl = null,
    string? TikTokUrl = null,
    string? YouTubeUrl = null,
    string? OtherLinkLabel = null,
    string? OtherLinkUrl = null,
    string? BannerUrl = null,
    string? ProfilePhotoUrl = null,
    long ProfileViewCount = 0,
    string? CertificationsJson = null);

public sealed record BuyerProfessionalProfileUpsertRequest(
    Guid? TenantId,
    string? Name,
    string? Phone,
    string? Specialty,
    string? Bio,
    bool? Activate,
    string? WebsiteUrl = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null,
    string? LinkedInUrl = null,
    string? XUrl = null,
    string? TikTokUrl = null,
    string? YouTubeUrl = null,
    string? OtherLinkLabel = null,
    string? OtherLinkUrl = null);

public sealed record BuyerFavoriteCreateRequest(
    string Type,
    Guid TargetId,
    Guid? TenantId
);

public sealed record BuyerFavoriteItem(
    Guid Id,
    string Type,
    Guid TargetId,
    Guid CustomerId,
    Guid? PartnerId,
    string? Name,
    string? Summary,
    string? Category,
    double? Price,
    string? Currency,
    string? ImageUrl,
    bool IsAvailable,
    DateTimeOffset CreatedAt
);

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
    string? Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool InStock = true,
    int? StockQuantity = null,
    int? AvailableQuantity = null,
    IReadOnlyList<string>? ImageUrls = null,
    string? PartnerName = null,
    string? PartnerLogoUrl = null
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
    DateTimeOffset UpdatedAt,
    bool IsBookable = false,
    bool RequiresOnlinePayment = false,
    string? ImageUrl = null,
    string? ServiceAddress = null,
    string? PartnerAddress = null,
    IReadOnlyList<string>? ImageUrls = null,
    IReadOnlyList<BuyerServiceImageInfo>? Images = null,
    string? PartnerName = null,
    string? PartnerLogoUrl = null
);

public sealed record BuyerServiceImageInfo(Guid Id, string Url, int SortOrder);

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
    DateTimeOffset CreatedAt,
    string? BannerUrl = null,
    string? ProfileHeadline = null,
    string? WebsiteUrl = null,
    string? InstagramUrl = null,
    string? FacebookUrl = null,
    string? LinkedInUrl = null,
    string? XUrl = null,
    string? TikTokUrl = null,
    string? YouTubeUrl = null,
    string? OtherLinkLabel = null,
    string? OtherLinkUrl = null,
    string? ProfilePhotoUrl = null,
    long ProfileViewCount = 0,
    string? CertificationsJson = null,
    long FollowerCount = 0
);

public sealed record ProfessionalCertification(
    string Id,
    string Name,
    string? Institution = null,
    int? Year = null,
    string? Url = null
);

public sealed record BuyerAddCertificationRequest(
    string Name,
    string? Institution = null,
    int? Year = null,
    string? Url = null
);

public sealed record ProfessionalFollowItem(
    Guid FollowId,
    string FollowedType,
    Guid FollowedId,
    string? Name,
    string? Subtitle,
    string? PhotoUrl,
    DateTimeOffset FollowedAt
);

public sealed record FollowStatusResponse(bool IsFollowing);

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
    string Name,
    double? Latitude,
    double? Longitude
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
    string Name,
    string? ImageUrl
);

public sealed record PublicSiteContentResponse(
    PublicHomeContent Home,
    PublicFooterContent Footer
);

public sealed record PublicHomeContent(
    string Title,
    string Subtitle,
    string BackgroundImageUrl,
    string PrimaryCtaLabel,
    string PrimaryCtaHref,
    string SecondaryCtaLabel,
    string SecondaryCtaHref
);

public sealed record PublicFooterContent(
    string CopyrightText,
    string InstagramUrl,
    string FacebookUrl,
    string LinkedInUrl
);

public sealed record PublicSubcategoryItem(
    Guid Id,
    Guid CategoryId,
    string Code,
    string Name
);

public sealed record InboxThreadCreateRequest(
    Guid? TenantId,
    Guid? PartnerId,
    Guid? ProfessionalId,
    string Subject,
    string Body);

public sealed record InboxMessageCreateRequest(string Body);

public sealed record InboxThreadStatusRequest(string Status);

public sealed record InboxThreadCreateResult(Guid ThreadId, bool Reused);

public sealed record GuestInboxThreadCreateRequest(
    Guid? PartnerId,
    Guid? ProfessionalId,
    string FullName,
    string Phone,
    string? Email,
    string? Subject,
    string Body);

public sealed record InboxThreadListItem(
    Guid Id,
    string? Subject,
    string? Status,
    DateTimeOffset LastMessageAt,
    string? Preview,
    bool Unread,
    string? CounterpartyName,
    string? CounterpartySubtitle,
    Guid? PartnerId,
    Guid? ProfessionalId);

public sealed record InboxThreadDetail(
    Guid Id,
    string? Subject,
    string? Status,
    DateTimeOffset LastMessageAt,
    string? CounterpartyName,
    string? CounterpartySubtitle,
    string? CustomerName,
    Guid? PartnerId,
    Guid? ProfessionalId,
    string? CustomerPhone = null,
    string? PartnerPhone = null,
    string? ProfessionalPhone = null);

public sealed record InboxThreadDetailResponse(
    InboxThreadDetail? Thread,
    IReadOnlyList<InboxMessageItem>? Messages);

public sealed record InboxMessageItem(
    Guid Id,
    string? SenderRole,
    string? Body,
    DateTimeOffset CreatedAt);

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
    string? Phone,
    string? ProfilePhotoUrl = null
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
    string? LogoUrl,
    string? CategoryName,
    string? SubcategoryName,
    Guid? ComunaId,
    string? ComunaName,
    double Latitude,
    double Longitude,
    bool UsesExactLocation,
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
    string? OfferLabel,
    /// <summary>Código de categoría (misma clave que en <c>/categorias/{slug}</c>).</summary>
    string? CategoryCode = null,
    string? BannerUrl = null,
    string? LogoUrl = null,
    string? StorefrontTagline = null,
    string? StorefrontAbout = null,
    string? StorefrontHighlight1 = null,
    string? StorefrontHighlight2 = null,
    string? StorefrontHighlight3 = null,
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

public sealed record PartnerProfileProduct(
    Guid Id,
    string? Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    string? ImageUrl = null,
    IReadOnlyList<string>? ImageUrls = null
);

public sealed record PartnerProfileService(
    Guid Id,
    string? Name,
    string? Description,
    string? Category,
    double Price,
    string? Currency,
    int DurationMinutes,
    string? ImageUrl = null,
    IReadOnlyList<string>? ImageUrls = null,
    string? ServiceAddress = null,
    bool IsBookable = false,
    bool RequiresOnlinePayment = false
);

public sealed record PartnerProfileProfessional(
    Guid Id,
    string? Name,
    string? Specialty,
    string? Bio,
    string? Email,
    string? Phone,
    string? BannerUrl = null,
    string? ProfileHeadline = null,
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
