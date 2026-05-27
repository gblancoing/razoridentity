using ComunaClick.Api.Modules.Orders.Contracts;
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
    private readonly IOrderCheckoutService _orderCheckoutService;
    private readonly IProductInventoryService _inventoryService;

    public OrdersController(
        CoreDbContext db,
        ITenantContext tenantContext,
        IOrderCheckoutService orderCheckoutService,
        IProductInventoryService inventoryService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _orderCheckoutService = orderCheckoutService;
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
    public async Task<ActionResult<Order>> Create(OrderCreateRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var result = await _orderCheckoutService.CreateOrderAsync(
            tenantId.Value,
            request.CustomerId,
            request,
            cancellationToken);

        if (!result.Success || result.Order is null)
        {
            return BadRequest(new { message = result.ErrorMessage ?? "Could not create order." });
        }

        return Created($"/v1/orders/{result.Order.Id}", result.Order);
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
