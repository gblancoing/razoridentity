using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ComunaClick.Api.Modules.Crm.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

[ApiController]
[Authorize(Policy = "buyer.profile")]
[Route("v1/buyer/customer")]
public sealed class BuyerCustomerController : ControllerBase
{
    private readonly CoreDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly CustomerAvatarStorage _avatarStorage;
    private readonly IConfiguration _configuration;
    private readonly ICustomerLinkService _customerLinkService;

    public BuyerCustomerController(
        CoreDbContext db,
        ITenantContext tenantContext,
        CustomerAvatarStorage avatarStorage,
        IConfiguration configuration,
        ICustomerLinkService customerLinkService)
    {
        _db = db;
        _tenantContext = tenantContext;
        _avatarStorage = avatarStorage;
        _configuration = configuration;
        _customerLinkService = customerLinkService;
    }

    [HttpPost("link-guest")]
    public async Task<ActionResult<Customer>> LinkGuest([FromBody] LinkGuestCustomerRequest request)
    {
        var tenantId = request.TenantId ?? _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        if (request.CustomerId == Guid.Empty)
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var (customer, error) = await _customerLinkService.LinkGuestCustomerAsync(
            request.CustomerId,
            tenantId.Value,
            email,
            ResolveName(User));

        if (customer is null)
        {
            return BadRequest(new { message = error ?? "Could not link guest profile." });
        }

        return Ok(customer);
    }

    [HttpPost("ensure")]
    public async Task<ActionResult<Customer>> Ensure([FromBody] BuyerCustomerEnsureRequest? request)
    {
        var tenantId = request?.TenantId
            ?? _tenantContext.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var fullName = ResolveName(User);

        var customer = await _db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);

        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = tenantId.Value,
                Email = normalizedEmail,
                FullName = fullName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _db.Customers.Add(customer);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                _db.Entry(customer).State = EntityState.Detached;
                customer = await _db.Customers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);
                if (customer is null)
                {
                    return BadRequest(new { message = "Unable to create buyer profile for selected tenant." });
                }
            }

            return Ok(customer);
        }

        var changed = false;
        if (string.IsNullOrWhiteSpace(customer.FullName) && !string.IsNullOrWhiteSpace(fullName))
        {
            customer.FullName = fullName;
            changed = true;
        }

        if (changed)
        {
            customer.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync();
        }

        return Ok(customer);
    }

    [HttpGet]
    public async Task<ActionResult<Customer>> GetCurrent([FromQuery] Guid? tenantId)
    {
        var tid = tenantId ?? _tenantContext.TenantId;
        if (!tid.HasValue || tid.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);

        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPatch]
    public async Task<ActionResult<Customer>> UpdateProfile(
        [FromBody] CustomerUpdateRequest request,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var tid = tenantId ?? _tenantContext.TenantId;
        if (!tid.HasValue || tid.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.FirstOrDefaultAsync(x =>
            x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == normalizedEmail);

        if (customer is null)
        {
            return NotFound();
        }

        if (request.Email is not null)
        {
            customer.Email = request.Email.Trim().ToLowerInvariant();
        }

        if (request.Phone is not null)
        {
            customer.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        }

        if (request.FullName is not null)
        {
            customer.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
        }

        if (request.AvatarUrl is not null)
        {
            customer.AvatarUrl = CustomerProfileHelper.SanitizeAvatarUrl(request.AvatarUrl);
        }

        if (request.UpdateDeliveryAddress)
        {
            var (countryId, regionId, comunaId) = await CustomerAddressHelper.NormalizeGeoAsync(
                _db, request.CountryId, request.RegionId, request.ComunaId, cancellationToken);

            if (!await CustomerAddressHelper.ValidateGeoAsync(
                    _db, countryId, regionId, comunaId, cancellationToken))
            {
                return BadRequest(new { message = "Invalid country, region or comuna." });
            }

            customer.CountryId = countryId is Guid c && c != Guid.Empty ? c : null;
            customer.RegionId = regionId is Guid r && r != Guid.Empty ? r : null;
            customer.ComunaId = comunaId is Guid co && co != Guid.Empty ? co : null;
            customer.Address = CustomerAddressHelper.NormalizeAddress(request.Address);
            customer.Latitude = NormalizeCoordinate(request.Latitude);
            customer.Longitude = NormalizeCoordinate(request.Longitude);
        }

        customer.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "No se pudo guardar el perfil. Si el error persiste, contacte soporte.",
                detail = ex.InnerException?.Message ?? ex.Message
            });
        }

        return Ok(customer);
    }

    private static double? NormalizeCoordinate(double? value)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return null;
        return Math.Round(value.Value, 6);
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(5_242_880)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5_242_880)]
    public async Task<ActionResult<Customer>> UploadAvatar(
        IFormFile file,
        [FromQuery] Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return BadRequest(new { message = "Image file is required." });
        }

        var tid = tenantId ?? _tenantContext.TenantId;
        if (!tid.HasValue || tid.Value == Guid.Empty)
        {
            return BadRequest(new { message = "TenantId is required." });
        }

        var email = ResolveEmail(User);
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { message = "Authenticated email claim is required." });
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var customer = await _db.Customers.FirstOrDefaultAsync(x =>
            x.TenantId == tid.Value && x.Email != null && x.Email.ToLower() == normalizedEmail, cancellationToken);

        if (customer is null)
        {
            return NotFound();
        }

        var avatarUrl = await _avatarStorage.SaveAsync(tid.Value, customer.Id, file, cancellationToken);
        if (avatarUrl is null)
        {
            return BadRequest(new { message = "Invalid image. Use JPG, PNG, WEBP or GIF up to 4 MB." });
        }

        if (avatarUrl.StartsWith('/'))
        {
            var publicBase = _configuration["PublicApi:BaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(publicBase))
            {
                publicBase = $"{Request.Scheme}://{Request.Host}";
            }

            avatarUrl = $"{publicBase}{avatarUrl}";
        }

        customer.AvatarUrl = CustomerProfileHelper.SanitizeAvatarUrl(avatarUrl);
        customer.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(customer);
    }

    private static string? ResolveEmail(ClaimsPrincipal user)
    {
        return user.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
    }

    private static string? ResolveName(ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.FindFirst("name")?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Name)?.Value;
    }
}
