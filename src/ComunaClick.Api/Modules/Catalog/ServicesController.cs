using ComunaClick.Api.Geo;
using ComunaClick.Api.Modules.Catalog.Contracts;
using ComunaClick.Api.Modules.Crm;
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
[Route("v1/services")]
public sealed class ServicesController : ControllerBase
{
    private static readonly HashSet<string> BlockingBookingStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "payment_pending",
        "confirmed",
        "paid",
        "scheduled"
    };

    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly ServiceImageStorage _imageStorage;

    public ServicesController(CoreDbContext db, ITenantContext tenantContext, ServiceImageStorage imageStorage)
    {
        _db = db;
        _tenantContext = tenantContext;
        _imageStorage = imageStorage;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Service>> Get(Guid id)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (service is null)
        {
            return NotFound();
        }

        await EnrichServiceAsync(service, HttpContext.RequestAborted);
        return Ok(service);
    }

    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [HttpGet("/v1/public/services/{id:guid}")]
    public async Task<ActionResult<Service>> GetPublic(Guid id)
    {
        var service = await _db.Services.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive && x.DeletedAt == null);

        if (service is null)
        {
            return NotFound();
        }

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == service.PartnerId);

        if (partner is null)
        {
            return NotFound();
        }

        if (!partner.IsVisible)
        {
            if (!await PartnerAccessAuthorization.CanPreviewUnpublishedPartnerAsync(
                    _db,
                    User,
                    service.PartnerId,
                    HttpContext.RequestAborted))
            {
                return NotFound();
            }
        }

        service.PartnerAddress = partner.Address;
        service.PartnerName = partner.Name;
        service.PartnerLogoUrl = partner.LogoUrl;
        await EnrichServiceAsync(service, HttpContext.RequestAborted);
        return Ok(service);
    }

    [HttpGet("/v1/partners/{partnerId:guid}/services")]
    public async Task<ActionResult<IEnumerable<Service>>> ListByPartner(Guid partnerId)
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

        var services = await _db.Services.AsNoTracking()
            .Where(x => x.PartnerId == partnerId && x.DeletedAt == null)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(HttpContext.RequestAborted);

        await EnrichServicesAsync(services, HttpContext.RequestAborted);
        return Ok(services);
    }

    [HttpPost]
    public async Task<ActionResult<Service>> Create(ServiceCreateRequest request)
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

        if (request.Price > 0 && (request.DurationMinutes ?? 0) < 15)
        {
            return BadRequest(new { message = "Los servicios reservables requieren duración mínima de 15 minutos." });
        }

        var tenantGeo = await GeoContextResolver.ResolveFromTenantAsync(_db, tenantId.Value);
        var (countryId, regionId, comunaId) = await CustomerAddressHelper.NormalizeGeoAsync(
            _db,
            request.CountryId ?? tenantGeo.CountryId,
            request.RegionId ?? tenantGeo.RegionId,
            request.ComunaId ?? tenantGeo.ComunaId,
            HttpContext.RequestAborted);

        if (comunaId is not Guid cid || cid == Guid.Empty)
        {
            return BadRequest(new { message = "Seleccioná país, región y comuna para el aviso." });
        }

        if (!await CustomerAddressHelper.ValidateGeoAsync(_db, countryId, regionId, comunaId, HttpContext.RequestAborted))
        {
            return BadRequest(new { message = "La ubicación seleccionada no es válida." });
        }

        if (!HasValidCoordinates(request.Latitude, request.Longitude))
        {
            return BadRequest(new { message = "Marcá el punto exacto del aviso en el mapa o usá «Detectar mi ubicación»." });
        }

        var service = new Service
        {
            TenantId = tenantId.Value,
            PartnerId = partnerId,
            CountryId = countryId,
            RegionId = regionId,
            ComunaId = comunaId,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            Name = request.Name.Trim(),
            Description = request.Description,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
            ServiceAddress = await ServiceGeoLabelBuilder.BuildAsync(_db, comunaId, HttpContext.RequestAborted)
                ?? NormalizeAddress(request.ServiceAddress),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "CLP" : request.Currency.Trim(),
            DurationMinutes = request.DurationMinutes ?? 30,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        ServiceCatalogPricing.Apply(service, request.Price);

        var staffError = await ServiceStaffSync.ValidateBookableStaffAsync(
            _db,
            partnerId,
            tenantId.Value,
            service.IsBookable,
            request.ProfessionalIds,
            HttpContext.RequestAborted);
        if (staffError is not null)
        {
            return BadRequest(new { message = staffError });
        }

        _db.Services.Add(service);
        await _db.SaveChangesAsync();

        await ServiceStaffSync.ReplaceAsync(_db, service, request.ProfessionalIds, HttpContext.RequestAborted);
        await _db.SaveChangesAsync();

        await EnrichServiceAsync(service, HttpContext.RequestAborted);
        return Created($"/v1/services/{service.Id}", service);
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<Service>> Update(Guid id, ServiceUpdateRequest request)
    {
        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (service is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            service.Name = request.Name.Trim();
        }

        if (request.Description is not null)
        {
            service.Description = request.Description;
        }

        if (request.Category is not null)
        {
            service.Category = request.Category;
        }

        if (request.ImageUrl is not null)
        {
            service.ImageUrl = request.ImageUrl;
        }

        if (request.CountryId.HasValue || request.RegionId.HasValue || request.ComunaId.HasValue)
        {
            var (countryId, regionId, comunaId) = await CustomerAddressHelper.NormalizeGeoAsync(
                _db,
                request.CountryId ?? service.CountryId,
                request.RegionId ?? service.RegionId,
                request.ComunaId ?? service.ComunaId,
                HttpContext.RequestAborted);

            if (comunaId is not Guid cid || cid == Guid.Empty)
            {
                return BadRequest(new { message = "Seleccioná país, región y comuna para el aviso." });
            }

            if (!await CustomerAddressHelper.ValidateGeoAsync(_db, countryId, regionId, comunaId, HttpContext.RequestAborted))
            {
                return BadRequest(new { message = "La ubicación seleccionada no es válida." });
            }

            service.CountryId = countryId;
            service.RegionId = regionId;
            service.ComunaId = comunaId;
            service.ServiceAddress = await ServiceGeoLabelBuilder.BuildAsync(_db, comunaId, HttpContext.RequestAborted);
        }

        if (request.Latitude.HasValue || request.Longitude.HasValue)
        {
            if (!HasValidCoordinates(request.Latitude, request.Longitude))
            {
                return BadRequest(new { message = "Marcá el punto exacto del aviso en el mapa o usá «Detectar mi ubicación»." });
            }

            service.Latitude = request.Latitude!.Value;
            service.Longitude = request.Longitude!.Value;
        }
        else if (request.ServiceAddress is not null)
        {
            service.ServiceAddress = NormalizeAddress(request.ServiceAddress);
        }

        if (request.Price.HasValue)
        {
            if (request.Price.Value > 0 && (request.DurationMinutes ?? service.DurationMinutes) < 15)
            {
                return BadRequest(new { message = "Los servicios reservables requieren duración mínima de 15 minutos." });
            }

            var wasBookable = service.IsBookable;
            ServiceCatalogPricing.Apply(service, request.Price.Value);
            if (wasBookable && !service.IsBookable)
            {
                var hasFuturePaid = await HasBlockingFutureBookingsAsync(service.Id);
                if (hasFuturePaid)
                {
                    return Conflict(new { message = "No se puede quitar el precio: hay reservas pagadas futuras." });
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            service.Currency = request.Currency.Trim();
        }

        if (request.DurationMinutes.HasValue)
        {
            service.DurationMinutes = request.DurationMinutes.Value;
        }

        if (request.IsActive.HasValue)
        {
            service.IsActive = request.IsActive.Value;
        }

        service.UpdatedAt = DateTimeOffset.UtcNow;

        var staffIdsForValidation = request.ProfessionalIds
            ?? (service.IsBookable
                ? await ServiceStaffSync.LoadProfessionalIdsAsync(_db, service.Id, HttpContext.RequestAborted)
                : null);

        var staffError = await ServiceStaffSync.ValidateBookableStaffAsync(
            _db,
            service.PartnerId,
            service.TenantId,
            service.IsBookable,
            staffIdsForValidation,
            HttpContext.RequestAborted);
        if (staffError is not null)
        {
            return BadRequest(new { message = staffError });
        }

        await _db.SaveChangesAsync();

        if (request.ProfessionalIds is not null)
        {
            await ServiceStaffSync.ReplaceAsync(_db, service, request.ProfessionalIds, HttpContext.RequestAborted);
            await _db.SaveChangesAsync();
        }
        else if (!service.IsBookable)
        {
            await ServiceStaffSync.ReplaceAsync(_db, service, null, HttpContext.RequestAborted);
            await _db.SaveChangesAsync();
        }

        await EnrichServiceAsync(service, HttpContext.RequestAborted);
        return Ok(service);
    }

    [HttpPatch("{id:guid}/geo")]
    public async Task<ActionResult<Service>> UpdateGeo(Guid id, ServiceGeoPatchRequest request)
    {
        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (service is null)
        {
            return NotFound();
        }

        if (!await CanManageServiceAsync(service))
        {
            return Forbid();
        }

        if (!HasValidCoordinates(request.Latitude, request.Longitude))
        {
            return BadRequest(new { message = "Marcá el punto exacto del aviso en el mapa o usá «Detectar mi ubicación»." });
        }

        var (countryId, regionId, comunaId) = await CustomerAddressHelper.NormalizeGeoAsync(
            _db,
            request.CountryId ?? service.CountryId,
            request.RegionId ?? service.RegionId,
            request.ComunaId ?? service.ComunaId,
            HttpContext.RequestAborted);

        if (comunaId is not Guid cid || cid == Guid.Empty)
        {
            return BadRequest(new { message = "Seleccioná país, región y comuna para el aviso." });
        }

        if (!await CustomerAddressHelper.ValidateGeoAsync(_db, countryId, regionId, comunaId, HttpContext.RequestAborted))
        {
            return BadRequest(new { message = "La ubicación seleccionada no es válida." });
        }

        service.CountryId = countryId;
        service.RegionId = regionId;
        service.ComunaId = comunaId;
        service.Latitude = request.Latitude;
        service.Longitude = request.Longitude;
        service.ServiceAddress = await ServiceGeoLabelBuilder.BuildAsync(_db, comunaId, HttpContext.RequestAborted);
        service.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        await EnrichServiceAsync(service, HttpContext.RequestAborted);
        return Ok(service);
    }

    [HttpPost("{id:guid}/images")]
    [RequestFormLimits(MultipartBodyLengthLimit = 20_000_000)]
    [RequestSizeLimit(20_000_000)]
    public async Task<ActionResult<IReadOnlyList<ServiceImageResponse>>> UploadImages(Guid id, [FromForm] List<IFormFile> files)
    {
        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (service is null)
        {
            return NotFound();
        }

        if (!await CanManageServiceAsync(service))
        {
            return Forbid();
        }

        if (files.Count == 0)
        {
            return BadRequest(new { message = "Seleccioná al menos una imagen." });
        }

        var existingCount = await _db.ServiceImages.CountAsync(x => x.ServiceId == id);
        if (existingCount + files.Count > _imageStorage.MaxImagesPerServiceLimit)
        {
            return BadRequest(new { message = $"Máximo {_imageStorage.MaxImagesPerServiceLimit} imágenes por aviso." });
        }

        var created = new List<ServiceImageResponse>();
        var sortOrder = existingCount;
        foreach (var file in files)
        {
            if (file.Length <= 0)
            {
                continue;
            }

            var imageId = Guid.NewGuid();
            var url = await _imageStorage.SaveAsync(service.TenantId, service.Id, imageId, file, HttpContext.RequestAborted);
            if (url is null)
            {
                continue;
            }

            var entity = new ServiceImage
            {
                Id = imageId,
                TenantId = service.TenantId,
                ServiceId = service.Id,
                Url = url,
                SortOrder = sortOrder++,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _db.ServiceImages.Add(entity);
            created.Add(new ServiceImageResponse(entity.Id, entity.Url, entity.SortOrder));
        }

        if (created.Count == 0)
        {
            return BadRequest(new { message = "No se pudieron guardar las imágenes. Usá JPG, PNG o WebP (máx. 4 MB c/u)." });
        }

        await _db.SaveChangesAsync();
        await ServiceMediaSync.SyncPrimaryImageUrlAsync(_db, service.Id, HttpContext.RequestAborted);

        return Ok(created);
    }

    [HttpDelete("{id:guid}/images/{imageId:guid}")]
    public async Task<IActionResult> DeleteImage(Guid id, Guid imageId)
    {
        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (service is null)
        {
            return NotFound();
        }

        if (!await CanManageServiceAsync(service))
        {
            return Forbid();
        }

        var image = await _db.ServiceImages.FirstOrDefaultAsync(x => x.Id == imageId && x.ServiceId == id);
        if (image is null)
        {
            return NotFound();
        }

        _db.ServiceImages.Remove(image);
        await _db.SaveChangesAsync();
        _imageStorage.DeletePhysical(service.TenantId, service.Id, image.Id, image.Url);
        await ServiceMediaSync.SyncPrimaryImageUrlAsync(_db, service.Id, HttpContext.RequestAborted);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var service = await _db.Services.FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt == null);
        if (service is null)
        {
            return NotFound();
        }

        if (await HasBlockingFutureBookingsAsync(service.Id))
        {
            return Conflict(new { message = "No se puede eliminar: hay reservas pagadas futuras. Pausá el aviso en su lugar." });
        }

        service.DeletedAt = DateTimeOffset.UtcNow;
        service.IsActive = false;
        service.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private async Task EnrichServiceAsync(Service service, CancellationToken cancellationToken)
    {
        await ServiceStaffSync.EnrichAsync(_db, service, cancellationToken);
        await ServiceMediaSync.EnrichAsync(_db, service, cancellationToken);
    }

    private async Task EnrichServicesAsync(IList<Service> services, CancellationToken cancellationToken)
    {
        await ServiceStaffSync.EnrichManyAsync(_db, services, cancellationToken);
        await ServiceMediaSync.EnrichManyAsync(_db, services, cancellationToken);
    }

    private async Task<bool> CanManageServiceAsync(Service service)
    {
        if (_tenantContext.PartnerId.HasValue && service.PartnerId != _tenantContext.PartnerId.Value)
        {
            return false;
        }

        if (_tenantContext.TenantId.HasValue && service.TenantId != _tenantContext.TenantId.Value)
        {
            return false;
        }

        if (_tenantContext.PartnerId.HasValue || _tenantContext.TenantId.HasValue)
        {
            return true;
        }

        var access = await PartnerAccessAuthorization.EnsurePartnerAccessAsync(
            _db,
            User,
            service.PartnerId,
            _tenantContext.TenantId,
            _tenantContext.PartnerId,
            HttpContext.RequestAborted);

        return access == PartnerAccessResult.Allowed;
    }

    private static string? NormalizeAddress(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<bool> HasBlockingFutureBookingsAsync(Guid serviceId)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.Bookings.AsNoTracking()
            .AnyAsync(x =>
                x.ServiceId == serviceId
                && x.StartAt > now
                && x.Amount > 0
                && BlockingBookingStatuses.Contains(x.Status));
    }

    private static bool HasValidCoordinates(double? latitude, double? longitude)
    {
        if (!latitude.HasValue && !longitude.HasValue)
        {
            return false;
        }

        if (!latitude.HasValue || !longitude.HasValue)
        {
            return false;
        }

        return latitude.Value is >= -90 and <= 90
            && longitude.Value is >= -180 and <= 180;
    }
}
