using ComunaClick.Shared.Http;
using System.Net.Http.Json;

namespace ComunaClick.Shared.Api.Buyer;

public sealed class BuyerApiClient : ApiClientBase
{
    public BuyerApiClient(HttpClient httpClient, Auth.Interfaces.ITokenStore tokenStore, Auth.Interfaces.IAuthClient authClient)
        : base(httpClient, tokenStore, authClient)
    {
    }

    public Task<IReadOnlyList<SearchResultItem>?> SearchAsync(string? query, string? type, int? limit, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var path = $"/v1/search?query={Uri.EscapeDataString(query ?? string.Empty)}&type={Uri.EscapeDataString(type ?? string.Empty)}&limit={(limit ?? 20)}";
        return GetWithTenantAsync(path, tenantId, cancellationToken);
    }

    public Task<IReadOnlyList<SearchResultItem>?> SearchPartnersAsync(string? query, int? limit, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var path = $"/v1/search/partners?query={Uri.EscapeDataString(query ?? string.Empty)}&limit={(limit ?? 20)}";
        return GetWithTenantAsync(path, tenantId, cancellationToken);
    }

    public Task<IReadOnlyList<SearchResultItem>?> SearchProfessionalsAsync(string? query, bool? verified, int? limit, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        var path = $"/v1/search/professionals?query={Uri.EscapeDataString(query ?? string.Empty)}&verified={(verified ?? false)}&limit={(limit ?? 20)}";
        return GetWithTenantAsync(path, tenantId, cancellationToken);
    }

    public Task<Order?> CreateOrderAsync(OrderCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Order>("/v1/orders", request, cancellationToken);

    public Task<Booking?> CreateBookingAsync(BookingCreateRequest request, CancellationToken cancellationToken = default)
        => PostAsync<Booking>("/v1/bookings", request, cancellationToken);

    public Task<Order?> GetOrderAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Order>($"/v1/orders/{id}", cancellationToken);

    public Task<Booking?> GetBookingAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Booking>($"/v1/bookings/{id}", cancellationToken);

    public Task<Customer?> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Customer>($"/v1/customers/{id}", cancellationToken);

    public Task<Customer?> UpdateCustomerAsync(Guid id, CustomerUpdateRequest request, CancellationToken cancellationToken = default)
        => PatchAsync<Customer>($"/v1/customers/{id}", request, cancellationToken);

    public Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Product>($"/v1/products/{id}", cancellationToken);

    public Task<Service?> GetServiceAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Service>($"/v1/services/{id}", cancellationToken);

    public Task<Partner?> GetPartnerAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Partner>($"/v1/partners/{id}", cancellationToken);

    public Task<Professional?> GetProfessionalAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<Professional>($"/v1/professionals/{id}", cancellationToken);

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

    private Task<IReadOnlyList<SearchResultItem>?> GetWithTenantAsync(string path, Guid? tenantId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (tenantId is not null && tenantId != Guid.Empty)
        {
            request.Headers.Add("X-Tenant-Id", tenantId.Value.ToString());
        }
        return SendAsync<IReadOnlyList<SearchResultItem>>(request, cancellationToken);
    }
}

public sealed record SearchResultItem(
    string? Type,
    Guid Id,
    string? Name,
    string? Category,
    Guid? PartnerId,
    double? Price,
    string? Currency
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
