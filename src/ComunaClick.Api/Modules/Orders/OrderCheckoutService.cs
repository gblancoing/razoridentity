using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Modules.Customers;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Types;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Orders;

public interface IOrderCheckoutService
{
    Task<OrderCheckoutResult> CreateOrderAsync(
        Guid tenantId,
        Guid customerId,
        OrderCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<(Customer Customer, OrderCheckoutResult Result)> CreateGuestOrderAsync(
        GuestOrderCreateRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record OrderCheckoutResult(
    bool Success,
    Order? Order,
    string? ErrorMessage,
    int? HttpStatus = null);

public sealed class OrderCheckoutService : IOrderCheckoutService
{
    private readonly CoreDbContext _db;
    private readonly IOrderNotificationService _orderNotificationService;
    private readonly IProductInventoryService _inventoryService;
    private readonly IGuestCustomerService _guestCustomers;

    public OrderCheckoutService(
        CoreDbContext db,
        IOrderNotificationService orderNotificationService,
        IProductInventoryService inventoryService,
        IGuestCustomerService guestCustomers)
    {
        _db = db;
        _orderNotificationService = orderNotificationService;
        _inventoryService = inventoryService;
        _guestCustomers = guestCustomers;
    }

    public async Task<OrderCheckoutResult> CreateOrderAsync(
        Guid tenantId,
        Guid customerId,
        OrderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.CustomerId != Guid.Empty && request.CustomerId != customerId)
        {
            return Fail("CustomerId does not match authenticated buyer.");
        }

        var normalized = request with { CustomerId = customerId };
        return await CreateOrderCoreAsync(tenantId, normalized, cancellationToken);
    }

    public async Task<(Customer Customer, OrderCheckoutResult Result)> CreateGuestOrderAsync(
        GuestOrderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = _guestCustomers.ValidateGuestContact(request.Guest);
        if (validation is not null)
        {
            return (null!, Fail(validation));
        }

        if (request.TenantId == Guid.Empty)
        {
            return (null!, Fail("TenantId is required."));
        }

        var customer = await _guestCustomers.EnsureGuestCustomerAsync(
            request.TenantId,
            request.Guest,
            request.DeliveryAddress,
            cancellationToken);
        if (customer is null)
        {
            return (null!, Fail("Could not create buyer profile for this purchase."));
        }

        var orderRequest = new OrderCreateRequest(
            request.PartnerId,
            customer.Id,
            request.DeliveryFee,
            request.Currency,
            request.Items.ToList(),
            DeliveryAddress: request.DeliveryAddress);

        var result = await CreateOrderCoreAsync(request.TenantId, orderRequest, cancellationToken);
        return (customer, result);
    }

    private async Task<OrderCheckoutResult> CreateOrderCoreAsync(
        Guid tenantId,
        OrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return Fail("Order must include at least one item.");
        }

        if (request.CustomerId == Guid.Empty)
        {
            return Fail("CustomerId is required.");
        }

        var hasCustomer = await _db.Customers.AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId && x.TenantId == tenantId, cancellationToken);

        if (!hasCustomer)
        {
            return Fail("CustomerId does not exist for current tenant.");
        }

        if (request.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0))
        {
            return Fail("Each item must include a valid ProductId and Quantity > 0.");
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.TenantId == tenantId && x.IsActive)
            .ToListAsync(cancellationToken);

        if (products.Count != productIds.Count)
        {
            return Fail("One or more products are not available for purchase.");
        }

        var stockCheck = await _inventoryService.ValidateLineItemsAsync(
            request.Items.Select(x => (x.ProductId, x.Quantity)).ToList(),
            cancellationToken: cancellationToken);
        if (!stockCheck.Ok)
        {
            return Fail(stockCheck.Message ?? "Insufficient stock.");
        }

        var partnerIds = products.Select(x => x.PartnerId).Distinct().ToList();
        if (partnerIds.Count != 1)
        {
            return Fail("All order items must belong to the same partner.");
        }

        var partnerId = partnerIds[0];
        if (request.PartnerId != Guid.Empty && request.PartnerId != partnerId)
        {
            return Fail("PartnerId does not match selected products.");
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId, cancellationToken);

        if (partner is null || !partner.IsVisible)
        {
            return Fail("Selected partner is not publicly available.");
        }

        DeliveryProvider? deliveryProvider = null;
        if (request.DeliveryProviderId.HasValue && request.DeliveryProviderId.Value != Guid.Empty)
        {
            deliveryProvider = await _db.DeliveryProviders.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.DeliveryProviderId.Value &&
                    x.IsActive &&
                    (!x.TenantId.HasValue || x.TenantId.Value == tenantId), cancellationToken);

            if (deliveryProvider is null)
            {
                return Fail("Selected delivery provider is not available.");
            }

            var providerAppliesToPartnerZone =
                (deliveryProvider.ComunaId.HasValue && partner.ComunaId.HasValue && deliveryProvider.ComunaId.Value == partner.ComunaId.Value) ||
                (!deliveryProvider.ComunaId.HasValue && deliveryProvider.RegionId.HasValue && partner.RegionId.HasValue && deliveryProvider.RegionId.Value == partner.RegionId.Value) ||
                (!deliveryProvider.ComunaId.HasValue && !deliveryProvider.RegionId.HasValue);

            if (!providerAppliesToPartnerZone)
            {
                return Fail("Delivery provider does not serve this business zone.");
            }
        }

        var productLookup = products.ToDictionary(x => x.Id);
        var items = request.Items.Select(item =>
        {
            var product = productLookup[item.ProductId];
            var total = product.Price * item.Quantity;
            return new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                TotalPrice = total
            };
        }).ToList();

        var subtotal = items.Sum(x => x.TotalPrice);
        var deliveryFee = deliveryProvider is null
            ? Math.Max(0, request.DeliveryFee)
            : Math.Max(deliveryProvider.BaseFee, request.DeliveryFee);
        var totalAmount = subtotal + deliveryFee;
        var currency = request.NormalizeCurrency(products[0].Currency);
        var subtotalMoney = new Money(subtotal, currency);
        var deliveryMoney = new Money(deliveryFee, currency);
        var totalMoney = new Money(totalAmount, currency);

        var order = new Order
        {
            TenantId = tenantId,
            PartnerId = partnerId,
            CustomerId = request.CustomerId,
            Status = "payment_pending",
            Subtotal = subtotalMoney.Amount,
            DeliveryFee = deliveryMoney.Amount,
            TotalAmount = totalMoney.Amount,
            Currency = totalMoney.Currency,
            DeliveryProviderId = deliveryProvider?.Id,
            DeliveryProviderName = deliveryProvider?.Name,
            DeliveryAddress = string.IsNullOrWhiteSpace(request.DeliveryAddress) ? null : request.DeliveryAddress.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items = items
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        order.ExternalReference = $"cc-order-{order.Id:N}";
        order.GrossAmount = totalMoney.Amount;
        order.NetAmount = totalMoney.Amount;
        order.PlatformFeeAmount = 0m;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == order.CustomerId && x.TenantId == order.TenantId, cancellationToken);

        if (customer is not null)
        {
            order.BuyerEmail = customer.Email;
            order.BuyerName = customer.FullName;
            await _orderNotificationService.NotifyPartnerAsync(order, partner, customer, items);
            await _orderNotificationService.NotifyBuyerOrderAsync(order, partner, customer, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new OrderCheckoutResult(true, order, null);
    }

    private static OrderCheckoutResult Fail(string message, int? status = null)
        => new(false, null, message, status);
}
