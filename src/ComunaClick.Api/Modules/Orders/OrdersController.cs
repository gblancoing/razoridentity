using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Orders;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public OrdersController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Order>> Get(Guid id)
    {
        var order = await _db.Orders
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);
        return order is null ? NotFound() : Ok(order);
    }

    [AllowAnonymous]
    [HttpGet("/v1/public/orders/{id:guid}")]
    public async Task<ActionResult<object>> GetPublic(Guid id)
    {
        var order = await _db.Orders.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

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
            order.CreatedAt,
            order.UpdatedAt,
            Partner = partner is null ? null : new
            {
                partner.Id,
                partner.Name,
                partner.Address,
                partner.Phone,
                partner.Email
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

    [HttpGet("/v1/partners/{partnerId:guid}/orders")]
    public async Task<ActionResult<IEnumerable<Order>>> ListByPartner(Guid partnerId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var orders = await _db.Orders
            .AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(orders);
    }

    [HttpPost]
    public async Task<ActionResult<Order>> Create(OrderCreateRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var partnerId = _tenantContext.PartnerId ?? request.PartnerId;
        if (partnerId == Guid.Empty)
        {
            return BadRequest(new { message = "PartnerId is required." });
        }

        if (_tenantContext.PartnerId.HasValue && request.PartnerId != Guid.Empty && request.PartnerId != _tenantContext.PartnerId.Value)
        {
            return Forbid();
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Order must include at least one item." });
        }

        var items = request.Items.Select(item =>
        {
            var total = item.UnitPrice * item.Quantity;
            return new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                TotalPrice = total
            };
        }).ToList();

        var subtotal = items.Sum(x => x.TotalPrice);
        var deliveryFee = request.DeliveryFee;
        var totalAmount = subtotal + deliveryFee;
        var currency = request.NormalizeCurrency("CLP");
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
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items = items
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();
        return Created($"/v1/orders/{order.Id}", order);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<Order>> UpdateStatus(Guid id, OrderStatusUpdateRequest request)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        order.Status = request.Status.Trim();
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<Order>> Cancel(Guid id)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(x => x.Id == id);
        if (order is null)
        {
            return NotFound();
        }

        order.Status = "cancelled";
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(order);
    }
}
