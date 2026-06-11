using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Types;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Orders;

[ApiController]
[Route("v1/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IOrderCheckoutService _orderCheckoutService;
    private readonly IProductInventoryService _inventoryService;
    private readonly IOrderNotificationService _orderNotificationService;
    private readonly IOrderTrackingTokenService _trackingTokens;
    private readonly IOptions<OrderTrackingOptions> _trackingOptions;

    public OrdersController(
        CoreDbContext db,
        ITenantContext tenantContext,
        IOrderCheckoutService orderCheckoutService,
        IProductInventoryService inventoryService,
        IOrderNotificationService orderNotificationService,
        IOrderTrackingTokenService trackingTokens,
        IOptions<OrderTrackingOptions> trackingOptions)
    {
        _db = db;
        _tenantContext = tenantContext;
        _orderCheckoutService = orderCheckoutService;
        _inventoryService = inventoryService;
        _orderNotificationService = orderNotificationService;
        _trackingTokens = trackingTokens;
        _trackingOptions = trackingOptions;
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
    public async Task<ActionResult<object>> GetPublic(
        Guid id,
        [FromQuery] Guid? customerId,
        [FromQuery] string? token)
    {
        // Acceso preferente por token firmado con expiración. El par id+customerId de los
        // enlaces antiguos solo se acepta durante el período de gracia (flag configurable).
        Guid resolvedCustomerId;
        if (!string.IsNullOrWhiteSpace(token))
        {
            if (!_trackingTokens.TryValidate(token, id, out resolvedCustomerId))
            {
                return NotFound();
            }
        }
        else if (_trackingOptions.Value.AllowLegacyPublicAccess
                 && customerId is { } legacyCustomerId
                 && legacyCustomerId != Guid.Empty)
        {
            resolvedCustomerId = legacyCustomerId;
        }
        else
        {
            return NotFound();
        }

        var order = await _db.Orders.AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.CustomerId == resolvedCustomerId);

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
            // Token fresco para que el frontend siga usando acceso por token (y deje de depender
            // del par id+customerId). Permite migrar y, luego, apagar AllowLegacyPublicAccess.
            TrackingToken = _trackingTokens.Create(order.Id, resolvedCustomerId),
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
            .Include(x => x.Items)
            .Where(x => x.TenantId == tenantId.Value && x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        // Nombre y teléfono del comprador para que el partner pueda contactarlo (ej. WhatsApp).
        var customerIds = orders.Select(x => x.CustomerId).Distinct().ToList();
        var customers = await _db.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.FullName, x.Phone })
            .ToDictionaryAsync(x => x.Id, x => x);
        foreach (var order in orders)
        {
            if (customers.TryGetValue(order.CustomerId, out var customer))
            {
                order.BuyerName ??= customer.FullName;
                order.BuyerPhone = customer.Phone;
            }
        }

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

        var order = await _db.Orders
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

        return await TransitionOrderAsync(order, request.Status, HttpContext.RequestAborted);
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

        var order = await _db.Orders
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

        return await TransitionOrderAsync(order, OrderStatusMachine.Cancelled, HttpContext.RequestAborted);
    }

    private async Task<ActionResult<Order>> TransitionOrderAsync(Order order, string requestedStatus, CancellationToken cancellationToken)
    {
        var newStatus = OrderStatusMachine.Normalize(requestedStatus);
        if (!OrderStatusMachine.CanTransition(order.Status, newStatus, out var error))
        {
            return BadRequest(new { message = error });
        }

        var previousStatus = OrderStatusMachine.Normalize(order.Status);
        if (string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase))
        {
            return Ok(order);
        }

        // Sin reservas, varias órdenes pendientes pueden apuntar al mismo stock:
        // al marcar pagada manualmente se exige stock físico suficiente.
        if (string.Equals(newStatus, OrderStatusMachine.Paid, StringComparison.OrdinalIgnoreCase)
            && order.InventoryFulfilledAt is null)
        {
            var stockCheck = await _inventoryService.ValidateLineItemsAsync(
                order.Items.Select(x => (x.ProductId, x.Quantity)).ToList(),
                cancellationToken: cancellationToken);
            if (!stockCheck.Ok)
            {
                return BadRequest(new { message = stockCheck.Message });
            }
        }

        order.Status = newStatus;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (string.Equals(newStatus, OrderStatusMachine.Paid, StringComparison.OrdinalIgnoreCase))
        {
            await OrderInventoryFulfillment.TryFulfillPaidOrderAsync(
                _db,
                _inventoryService,
                order,
                newStatus,
                cancellationToken);

            // Aviso de venta al comercio (encolado, idempotente): solo cuando la orden está pagada.
            await _orderNotificationService.NotifyPartnerOrderPaidAsync(order.Id, cancellationToken);
        }
        else if (string.Equals(newStatus, OrderStatusMachine.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            await HandleCancellationAsync(order, previousStatus, cancellationToken);
        }

        return Ok(order);
    }

    private async Task HandleCancellationAsync(Order order, string previousStatus, CancellationToken cancellationToken)
    {
        if (order.InventoryFulfilledAt is not null)
        {
            await _inventoryService.RestoreOrderAsync(order, cancellationToken);
        }

        // Orden cancelada después de pagada: dejar registrado que requiere reembolso
        // (la ejecución del reembolso es un proceso aparte).
        if (string.Equals(previousStatus, OrderStatusMachine.Paid, StringComparison.OrdinalIgnoreCase))
        {
            _db.Interactions.Add(new Interaction
            {
                TenantId = order.TenantId,
                CustomerId = order.CustomerId,
                PartnerId = order.PartnerId,
                Type = "order_refund_required",
                ReferenceId = order.Id,
                Payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    order.TotalAmount,
                    order.Currency,
                    previousStatus,
                    cancelledAt = DateTimeOffset.UtcNow
                }),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
