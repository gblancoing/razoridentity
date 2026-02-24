using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;

    public ProductsController(CoreDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Product>> Get(Guid id)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == id);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/products")]
    public async Task<ActionResult<IEnumerable<Product>>> ListByPartner(Guid partnerId)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != partnerId)
        {
            return Forbid();
        }

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return Ok(products);
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(ProductCreateRequest request)
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

        var product = new Product
        {
            TenantId = tenantId.Value,
            PartnerId = partnerId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Category = request.Category,
            Price = request.Price,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return Created($"/v1/products/{product.Id}", product);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Product>> Update(Guid id, ProductUpdateRequest request)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != product.PartnerId)
        {
            return Forbid();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            product.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            product.Description = request.Description;
        }

        if (request.Category is not null)
        {
            product.Category = request.Category;
        }

        if (request.Price.HasValue)
        {
            product.Price = request.Price.Value;
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            product.Currency = request.Currency.Trim();
        }

        if (request.IsActive.HasValue)
        {
            product.IsActive = request.IsActive.Value;
        }

        product.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(product);
    }

    [HttpPatch("{id:guid}/inventory")]
    public async Task<ActionResult<ProductInventory>> UpdateInventory(Guid id, ProductInventoryUpdateRequest request)
    {
        var product = await _db.Products.Include(x => x.Inventory).FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != product.PartnerId)
        {
            return Forbid();
        }

        var inventory = product.Inventory ?? new ProductInventory { ProductId = id };
        inventory.Quantity = request.Quantity;
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

        if (product.Inventory is null)
        {
            _db.ProductInventories.Add(inventory);
        }

        await _db.SaveChangesAsync();
        return Ok(inventory);
    }
}
