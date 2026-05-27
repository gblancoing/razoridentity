using ComunaClick.Api.Geo;
using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

[ApiController]
[Authorize(Policy = "partner.staff")]
[Route("v1/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ProductImageStorage _imageStorage;
    private readonly IProductInventoryService _inventoryService;

    public ProductsController(
        CoreDbContext db,
        ITenantContext tenantContext,
        ProductImageStorage imageStorage,
        IProductInventoryService inventoryService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _imageStorage = imageStorage;
        _inventoryService = inventoryService;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        await ProductMediaSync.EnrichAsync(_db, product, HttpContext.RequestAborted);
        return Ok(ProductResponses.ToPartnerResponse(product, includeCost: true));
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("/v1/public/products/{id:guid}")]
    public async Task<ActionResult<object>> GetPublic(Guid id)
    {
        var product = await _db.Products.AsNoTracking()
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);

        if (product is null)
        {
            return NotFound();
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == product.PartnerId);

        if (partner is null)
        {
            return NotFound();
        }

        if (!partner.IsVisible
            && !await PartnerAccessAuthorization.CanPreviewUnpublishedPartnerAsync(
                _db,
                User,
                product.PartnerId,
                HttpContext.RequestAborted))
        {
            return NotFound();
        }

        await ProductMediaSync.EnrichAsync(_db, product, HttpContext.RequestAborted);
        var available = await _inventoryService.GetAvailableQuantityAsync(product.Id, cancellationToken: HttpContext.RequestAborted);
        return Ok(ProductResponses.ToPublicResponse(product, partner.Name, partner.LogoUrl, available));
    }

    [HttpGet("/v1/partners/{partnerId:guid}/products")]
    public async Task<ActionResult<IEnumerable<object>>> ListByPartner(Guid partnerId)
    {
        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db,
            User,
            partnerId,
            _tenantContext.TenantId,
            _tenantContext.PartnerId,
            HttpContext.RequestAborted);

        if (access == PartnerAccessResult.NotFound)
        {
            return NotFound();
        }

        if (access == PartnerAccessResult.Forbidden)
        {
            return Forbid();
        }

        var products = await _db.Products.AsNoTracking()
            .Include(x => x.Inventory)
            .Where(x => x.PartnerId == partnerId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        await ProductMediaSync.EnrichManyAsync(_db, products, HttpContext.RequestAborted);
        return Ok(products.Select(x => ProductResponses.ToPartnerResponse(x, includeCost: true)));
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create(ProductCreateRequest request)
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

        if (request.Price < 0)
        {
            return BadRequest(new { message = "El precio de venta no puede ser negativo." });
        }

        var geo = await GeoContextResolver.ResolveFromTenantAsync(_db, tenantId.Value);
        var categoryLabel = await ResolveCategoryLabelAsync(request.Category, request.PartnerCatalogCategoryId, partnerId);

        var product = new Product
        {
            TenantId = tenantId.Value,
            PartnerId = partnerId,
            CountryId = geo.CountryId,
            RegionId = geo.RegionId,
            ComunaId = geo.ComunaId,
            Name = request.Name.Trim(),
            Description = request.Description,
            Category = categoryLabel,
            PartnerCatalogCategoryId = request.PartnerCatalogCategoryId,
            ImageUrl = null,
            Price = request.Price,
            CostPrice = request.CostPrice,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var initialStock = Math.Max(0, request.InitialStock ?? 0);
        await _inventoryService.EnsureInventoryRowAsync(product.Id, initialStock, HttpContext.RequestAborted);
        product.Inventory = await _db.ProductInventories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProductId == product.Id, HttpContext.RequestAborted);

        await ProductMediaSync.EnrichAsync(_db, product, HttpContext.RequestAborted);
        return Created($"/v1/products/{product.Id}", ProductResponses.ToPartnerResponse(product, includeCost: true));
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, ProductUpdateRequest request)
    {
        var product = await _db.Products.Include(x => x.Inventory).FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (!await CanManageProductAsync(product))
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

        if (request.PartnerCatalogCategoryId.HasValue || request.Category is not null)
        {
            product.PartnerCatalogCategoryId = request.PartnerCatalogCategoryId ?? product.PartnerCatalogCategoryId;
            product.Category = await ResolveCategoryLabelAsync(
                request.Category ?? product.Category,
                product.PartnerCatalogCategoryId,
                product.PartnerId);
        }

        if (request.Price.HasValue)
        {
            if (request.Price.Value < 0)
            {
                return BadRequest(new { message = "El precio de venta no puede ser negativo." });
            }

            product.Price = request.Price.Value;
        }

        if (request.CostPrice.HasValue)
        {
            product.CostPrice = request.CostPrice.Value < 0 ? null : request.CostPrice;
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
        await ProductMediaSync.EnrichAsync(_db, product, HttpContext.RequestAborted);
        return Ok(ProductResponses.ToPartnerResponse(product, includeCost: true));
    }

    [HttpPatch("{id:guid}/inventory")]
    public async Task<ActionResult<ProductInventory>> UpdateInventory(Guid id, ProductInventoryUpdateRequest request)
    {
        var product = await _db.Products.Include(x => x.Inventory).FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (!await CanManageProductAsync(product))
        {
            return Forbid();
        }

        if (request.Quantity < 0)
        {
            return BadRequest(new { message = "El stock no puede ser negativo." });
        }

        var inventory = product.Inventory ?? new ProductInventory { ProductId = id };
        var previousQuantity = inventory.Quantity;
        inventory.Quantity = Math.Max(0, request.Quantity);
        inventory.UpdatedAt = DateTimeOffset.UtcNow;

        if (product.Inventory is null)
        {
            _db.ProductInventories.Add(inventory);
        }

        await _db.SaveChangesAsync();
        await _inventoryService.NotifyStockLevelsAsync(
            product,
            previousQuantity,
            inventory.Quantity,
            "manual_update",
            product.Id,
            HttpContext.RequestAborted);
        return Ok(inventory);
    }

    [HttpPost("{id:guid}/images")]
    [RequestFormLimits(MultipartBodyLengthLimit = 20_000_000)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<IReadOnlyList<ProductImageResponse>>> UploadImages(Guid id, [FromForm] List<IFormFile> files)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (!await CanManageProductAsync(product))
        {
            return Forbid();
        }

        if (files.Count == 0)
        {
            return BadRequest(new { message = "Seleccioná al menos una imagen." });
        }

        var existingCount = await _db.ProductImages.CountAsync(x => x.ProductId == id);
        if (existingCount + files.Count > _imageStorage.MaxImagesPerProductLimit)
        {
            return BadRequest(new { message = $"Máximo {_imageStorage.MaxImagesPerProductLimit} imágenes por producto." });
        }

        var created = new List<ProductImageResponse>();
        var sortOrder = existingCount;
        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                continue;
            }

            var imageId = Guid.NewGuid();
            var url = await _imageStorage.SaveAsync(product.TenantId, product.Id, imageId, file, HttpContext.RequestAborted);
            if (url is null)
            {
                continue;
            }

            var entity = new ProductImage
            {
                Id = imageId,
                TenantId = product.TenantId,
                ProductId = product.Id,
                Url = url,
                SortOrder = sortOrder++,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.ProductImages.Add(entity);
            created.Add(new ProductImageResponse(entity.Id, entity.Url, entity.SortOrder));
        }

        if (created.Count == 0)
        {
            return BadRequest(new { message = "No se pudieron guardar las imágenes. Usá JPG, PNG o WebP (máx. 4 MB c/u)." });
        }

        await _db.SaveChangesAsync();
        await ProductMediaSync.SyncPrimaryImageUrlAsync(_db, product.Id, HttpContext.RequestAborted);
        return Ok(created);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId)
    {
        var product = await _db.Products.FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (!await CanManageProductAsync(product))
        {
            return Forbid();
        }

        var image = await _db.ProductImages.FirstOrDefaultAsync(x => x.Id == imageId && x.ProductId == id);
        if (image is null)
        {
            return NotFound();
        }

        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync();
        _imageStorage.DeletePhysical(product.TenantId, product.Id, image.Id, image.Url);
        await ProductMediaSync.SyncPrimaryImageUrlAsync(_db, product.Id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var product = await _db.Products
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        if (!await CanManageProductAsync(product))
        {
            return Forbid();
        }

        var hasOpenOrders = await (
            from item in _db.OrderItems.AsNoTracking()
            join order in _db.Orders.AsNoTracking() on item.OrderId equals order.Id
            where item.ProductId == id
                  && (order.Status == "payment_pending" || order.Status == "paid" || order.Status == "processing")
            select item.Id).AnyAsync();

        if (hasOpenOrders)
        {
            return BadRequest(new { message = "No se puede eliminar: hay pedidos activos con este producto. Pausalo en su lugar." });
        }

        var images = await _db.ProductImages.Where(x => x.ProductId == id).ToListAsync();
        _db.ProductImages.RemoveRange(images);
        if (product.Inventory is not null)
        {
            _db.ProductInventories.Remove(product.Inventory);
        }

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        foreach (var image in images)
        {
            _imageStorage.DeletePhysical(product.TenantId, product.Id, image.Id, image.Url);
        }

        return NoContent();
    }

    private async Task<bool> CanManageProductAsync(Product product)
    {
        var scopedPartner = _tenantContext.PartnerId;
        if (scopedPartner.HasValue && scopedPartner.Value != product.PartnerId)
        {
            return false;
        }

        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db,
            User,
            product.PartnerId,
            _tenantContext.TenantId,
            _tenantContext.PartnerId,
            HttpContext.RequestAborted);

        return access == PartnerAccessResult.Allowed;
    }

    private async Task<string?> ResolveCategoryLabelAsync(string? category, Guid? categoryId, Guid partnerId)
    {
        if (categoryId.HasValue)
        {
            var sectionName = await _db.PartnerCatalogCategories.AsNoTracking()
                .Where(x => x.Id == categoryId.Value && x.PartnerId == partnerId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                return sectionName.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(category) ? null : category.Trim();
    }
}
