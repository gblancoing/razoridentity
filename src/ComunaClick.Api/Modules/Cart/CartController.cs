using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Cart.Contracts;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Cart;

[ApiController]
[Authorize(Policy = "buyer.customer")]
[EnableRateLimiting("public-write")]
[Route("v1/cart")]
public sealed class CartController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly IOrderNotificationService _orderNotificationService;
    private readonly IProductInventoryService _inventoryService;

    public CartController(
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

    [HttpGet]
    public async Task<ActionResult<object>> GetCurrent(CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var customer = await EnsureCustomerAsync(tenantId.Value, cancellationToken);
        if (customer is null)
        {
            return BadRequest(new { message = "Unable to resolve customer profile." });
        }

        var carts = await _db.ShoppingCarts.AsNoTracking()
            .Where(x => x.TenantId == tenantId.Value && x.CustomerId == customer.Id && x.Status == "active")
            .Include(x => x.Items)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        var productIds = carts.SelectMany(x => x.Items).Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        return Ok(new
        {
            customerId = customer.Id,
            carts = carts.Select(cart => new
            {
                cart.Id,
                cart.PartnerId,
                cart.Status,
                cart.Subtotal,
                cart.DeliveryFee,
                cart.TotalAmount,
                cart.Currency,
                cart.CreatedAt,
                cart.UpdatedAt,
                items = cart.Items.Select(item => new
                {
                    item.Id,
                    item.ProductId,
                    productName = products.TryGetValue(item.ProductId, out var product) ? product.Name : null,
                    item.Quantity,
                    item.UnitPrice,
                    item.TotalPrice
                })
            })
        });
    }

    [HttpPost("items")]
    public async Task<ActionResult<object>> UpsertItem(CartItemUpsertRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (request.ProductId == Guid.Empty || request.Quantity <= 0)
        {
            return BadRequest(new { message = "ProductId and quantity are required." });
        }

        var customer = await EnsureCustomerAsync(tenantId.Value, cancellationToken);
        if (customer is null)
        {
            return BadRequest(new { message = "Unable to resolve customer profile." });
        }

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ProductId && x.TenantId == tenantId.Value && x.IsActive, cancellationToken);

        if (product is null)
        {
            return BadRequest(new { message = "Selected product is not available." });
        }

        var stockCheck = await _inventoryService.ValidateLineItemsAsync(
            [(product.Id, request.Quantity)],
            cancellationToken: cancellationToken);
        if (!stockCheck.Ok)
        {
            return BadRequest(new { message = stockCheck.Message });
        }

        var cart = await _db.ShoppingCarts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                x.CustomerId == customer.Id &&
                x.PartnerId == product.PartnerId &&
                x.Status == "active",
                cancellationToken);

        if (cart is null)
        {
            cart = new ShoppingCart
            {
                TenantId = tenantId.Value,
                CustomerId = customer.Id,
                PartnerId = product.PartnerId,
                Status = "active",
                Currency = string.IsNullOrWhiteSpace(product.Currency) ? "CLP" : product.Currency!,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.ShoppingCarts.Add(cart);
        }

        var currentItem = cart.Items.FirstOrDefault(x => x.ProductId == product.Id);
        if (currentItem is null)
        {
            currentItem = new ShoppingCartItem
            {
                ProductId = product.Id,
                Quantity = request.Quantity,
                UnitPrice = product.Price,
                TotalPrice = product.Price * request.Quantity,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            cart.Items.Add(currentItem);
        }
        else
        {
            currentItem.Quantity = request.Quantity;
            currentItem.UnitPrice = product.Price;
            currentItem.TotalPrice = product.Price * request.Quantity;
            currentItem.UpdatedAt = DateTimeOffset.UtcNow;
        }

        RecalculateCart(cart);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            cart.Id,
            cart.PartnerId,
            cart.Subtotal,
            cart.DeliveryFee,
            cart.TotalAmount,
            cart.Currency,
            items = cart.Items.Select(item => new
            {
                item.Id,
                item.ProductId,
                item.Quantity,
                item.UnitPrice,
                item.TotalPrice
            })
        });
    }

    [HttpDelete("items/{itemId:guid}")]
    public async Task<ActionResult<object>> RemoveItem(Guid itemId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var customer = await EnsureCustomerAsync(tenantId.Value, cancellationToken);
        if (customer is null)
        {
            return BadRequest(new { message = "Unable to resolve customer profile." });
        }

        var cartItem = await _db.ShoppingCartItems
            .Include(x => x.Cart)
            .FirstOrDefaultAsync(x =>
                x.Id == itemId &&
                x.Cart.TenantId == tenantId.Value &&
                x.Cart.CustomerId == customer.Id &&
                x.Cart.Status == "active",
                cancellationToken);

        if (cartItem is null)
        {
            return NotFound();
        }

        var cart = cartItem.Cart;
        _db.ShoppingCartItems.Remove(cartItem);
        await _db.SaveChangesAsync(cancellationToken);

        cart = await _db.ShoppingCarts.Include(x => x.Items).FirstAsync(x => x.Id == cart.Id, cancellationToken);
        RecalculateCart(cart);

        if (cart.Items.Count == 0)
        {
            _db.ShoppingCarts.Remove(cart);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Item removed from cart." });
    }

    [HttpPost("checkout")]
    public async Task<ActionResult<Order>> Checkout(CartCheckoutRequest request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var customer = await EnsureCustomerAsync(tenantId.Value, cancellationToken);
        if (customer is null)
        {
            return BadRequest(new { message = "Unable to resolve customer profile." });
        }

        var cart = await _db.ShoppingCarts
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x =>
                x.Id == request.CartId &&
                x.TenantId == tenantId.Value &&
                x.CustomerId == customer.Id &&
                x.Status == "active",
                cancellationToken);

        if (cart is null || cart.Items.Count == 0)
        {
            return BadRequest(new { message = "Cart is empty or not available." });
        }

        var orderRequest = new OrderCreateRequest(
            cart.PartnerId,
            customer.Id,
            request.DeliveryFee ?? cart.DeliveryFee,
            request.Currency ?? cart.Currency,
            cart.Items.Select(x => new OrderItemCreateRequest(x.ProductId, x.Quantity, x.UnitPrice)).ToList(),
            request.DeliveryProviderId,
            request.DeliveryAddress);

        var orderResult = await CreateOrderFromRequestAsync(orderRequest, tenantId.Value, cancellationToken);
        if (orderResult.Result is ObjectResult objectResult)
        {
            return objectResult;
        }

        var order = orderResult.Value!;
        cart.Status = "checked_out";
        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return Created($"/v1/orders/{order.Id}", order);
    }

    private async Task<ActionResult<Order>> CreateOrderFromRequestAsync(OrderCreateRequest request, Guid tenantId, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new { message = "Order must include at least one item." });
        }

        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.TenantId == tenantId && x.IsActive)
            .ToListAsync(cancellationToken);

        if (products.Count != productIds.Count)
        {
            return BadRequest(new { message = "One or more products are not available for purchase." });
        }

        var stockCheck = await _inventoryService.ValidateLineItemsAsync(
            request.Items.Select(x => (x.ProductId, x.Quantity)).ToList(),
            cancellationToken: cancellationToken);
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
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == partnerId && x.TenantId == tenantId && x.IsVisible, cancellationToken);
        if (partner is null)
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
                    (!x.TenantId.HasValue || x.TenantId.Value == tenantId),
                    cancellationToken);

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
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? (products[0].Currency ?? "CLP") : request.Currency.Trim();

        var order = new Order
        {
            TenantId = tenantId,
            PartnerId = partnerId,
            CustomerId = request.CustomerId,
            Status = "payment_pending",
            Subtotal = subtotal,
            DeliveryFee = deliveryFee,
            TotalAmount = totalAmount,
            Currency = currency,
            DeliveryProviderId = deliveryProvider?.Id,
            DeliveryProviderName = deliveryProvider?.Name,
            DeliveryAddress = string.IsNullOrWhiteSpace(request.DeliveryAddress) ? null : request.DeliveryAddress.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items = items
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == order.CustomerId && x.TenantId == tenantId, cancellationToken);
        if (customer is not null)
        {
            await _orderNotificationService.NotifyPartnerAsync(order, partner, customer, items, cancellationToken);
        }

        return order;
    }

    private async Task<Customer?> EnsureCustomerAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? User.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail, cancellationToken);

        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            TenantId = tenantId,
            Email = normalizedEmail,
            FullName = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private static void RecalculateCart(ShoppingCart cart)
    {
        cart.Subtotal = cart.Items.Sum(x => x.TotalPrice);
        cart.TotalAmount = cart.Subtotal + cart.DeliveryFee;
        cart.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
