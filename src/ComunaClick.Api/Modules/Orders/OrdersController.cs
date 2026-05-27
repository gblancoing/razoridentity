using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Orders;

[ApiController]
[Route("v1/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IOrderNotificationService _orderNotificationService;
    private readonly IProductInventoryService _inventoryService;

    public OrdersController(
        CoreDbContext db,
        ITenantContext tenantContext,
        IOrderNotificationService orderNotificationService,
        IProductInventoryService inventoryService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _orderNotificationService = orderNotificationService;
        _inventoryService = inventoryService;
    }

    [Authorize(Policy = "partner.staff")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> Get(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var order = await _db.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);

        if (order is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && _tenantContext.PartnerId.Value != order.PartnerId)
        {
            return Forbid();
        }

        return Ok(order);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("/v1/public/orders/{id:guid}")]
    public async Task<ActionResult<object>> GetPublic(Guid id, [FromQuery] Guid customerId)
    {
        if (customerId == Guid.Empty)
        {
            return NotFound();
        }

        var order = await _db.Orders.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId);

        if (order is null)
        {
            return NotFound();
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == order.PartnerId);

        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToList();
        var productNames = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        return Ok(new
        {
            order.Id,
            order.Status,
            order.Subtotal,
            order.DeliveryFee,
            order.TotalAmount,
            order.Currency,
            order.DeliveryProviderId,
            order.DeliveryProviderName,
            order.DeliveryAddress,
            order.CreatedAt,
            order.UpdatedAt,
            Partner = partner is null ? null : new
            {
                partner.Id,
                partner.Name,
                partner.Address,
                partner.Phone
            },
            Items = order.Items.Select(item => new
            {
                item.Id,
                item.ProductId,
                ProductName = productNames.TryGetValue(item.ProductId, out var productName) ? productName : null,
                item.Quantity,
                item.UnitPrice,
                item.TotalPrice
            })
        });
    }

    [Authorize(Policy = "partner.staff")]
    [HttpGet("/v1/partners/{partnerId:guid}/orders")]
    public async Task<ActionResult<IEnumerable<Order>>> ListByPartner(Guid partnerId)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var hasPartner = await _db.Partners.AsNoTracking()
            .AnyAsync(x => x.Id == partnerId && x.TenantId == tenantId.Value);
        if (!hasPartner)
        {
            return NotFound();
        }

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(orders);
    }

    [Authorize(Policy = "buyer.customer")]
    [EnableRateLimiting("public-write")]
    [HttpPost]
    public async Task<ActionResult<Order>> Create(OrderCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Order must include at least one item." });
        }

        if (request.CustomerId == Guid.Empty)
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        var hasCustomer = await _db.Customers.AsNoTracking()
            .AnyAsync(x => x.Id == request.CustomerId && x.TenantId == tenantId.Value);

        if (!hasCustomer)
        {
            return BadRequest(new { message = "CustomerId does not exist for current tenant." });
        }

        if (request.Items.Any(item => item.ProductId == Guid.Empty || item.Quantity <= 0))
        {
            return BadRequest(new { message = "Each item must include a valid ProductId and Quantity > 0." });
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.TenantId == tenantId.Value && x.IsActive)
            .ToListAsync();

        if (products.Count != productIds.Count)
        {
            return BadRequest(new { message = "One or more products are not available for purchase." });
        }

        var stockCheck = await _inventoryService.ValidateLineItemsAsync(
            request.Items.Select(x => (x.ProductId, x.Quantity)).ToList(),
            cancellationToken: HttpContext.RequestAborted);
        if (!stockCheck.Ok)
        {
            return BadRequest(new { message = stockCheck.Message });
        }

        var partnerIds = products.Select(x => x.PartnerId).Distinct().ToList();
        if (partnerIds.Count != 1)
        {
            return BadRequest(new { message = "All order items must belong to the same partner." });
        }

        var partnerId = partnerIds[0];
        if (request.PartnerId != Guid.Empty && request.PartnerId != partnerId)
        {
            return BadRequest(new { message = "PartnerId does not match selected products." });
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId.Value);

        if (partner is null || !partner.IsVisible)
        {
            return BadRequest(new { message = "Selected partner is not publicly available." });
        }

        DeliveryProvider? deliveryProvider = null;
        if (request.DeliveryProviderId.HasValue && request.DeliveryProviderId.Value != Guid.Empty)
        {
            deliveryProvider = await _db.DeliveryProviders.AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == request.DeliveryProviderId.Value &&
                    x.IsActive &&
                    (!x.TenantId.HasValue || x.TenantId.Value == tenantId.Value));

            if (deliveryProvider is null)
            {
                return BadRequest(new { message = "Selected delivery provider is not available." });
            }

            var providerAppliesToPartnerZone =
                (deliveryProvider.ComunaId.HasValue && partner.ComunaId.HasValue && deliveryProvider.ComunaId.Value == partner.ComunaId.Value) ||
                (!deliveryProvider.ComunaId.HasValue && deliveryProvider.RegionId.HasValue && partner.RegionId.HasValue && deliveryProvider.RegionId.Value == partner.RegionId.Value) ||
                (!deliveryProvider.ComunaId.HasValue && !deliveryProvider.RegionId.HasValue);

            if (!providerAppliesToPartnerZone)
            {
                return BadRequest(new { message = "Delivery provider does not serve this business zone." });
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
            TenantId = tenantId.Value,
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
        await _db.SaveChangesAsync();

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == order.CustomerId && x.TenantId == order.TenantId);

        if (customer is not null)
        {
            await _orderNotificationService.NotifyPartnerAsync(order, partner, customer, items);
        }

        return Created($"/v1/orders/{order.Id}", order);
    }

    [Authorize(Policy = "partner.staff")]
    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<Order>> UpdateStatus(Guid id, OrderStatusUpdateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (order is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && _tenantContext.PartnerId.Value != order.PartnerId)
        {
            return Forbid();
        }

        var previousStatus = order.Status;
        var newStatus = request.Status.Trim();
        order.Status = newStatus;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var orderWithItems = await _db.Orders
            .Include(x => x.Items)
            .FirstAsync(x => x.Id == order.Id, HttpContext.RequestAborted);
        await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(
            _db,
            _inventoryService,
            orderWithItems,
            previousStatus,
            newStatus,
            HttpContext.RequestAborted);

        return Ok(order);
    }

    [Authorize(Policy = "partner.staff")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<Order>> Cancel(Guid id)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId.Value);
        if (order is null)
        {
            return NotFound();
        }

        if (_tenantContext.PartnerId.HasValue && _tenantContext.PartnerId.Value != order.PartnerId)
        {
            return Forbid();
        }

        order.Status = "cancelled";
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }
}
