using System.Text;
using ComunaClick.Api.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

[ApiController]
[Route("v1/admin/orders")]
[Authorize(Policy = "platform.admin")]
public sealed class AdminOrdersController : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxExportRows = 2000;

    private readonly CoreDbContext _db;

    public AdminOrdersController(CoreDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<AdminPagedResult<AdminOrderListItemDto>>> List(
        [FromQuery] string? status,
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? partnerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = BuildQuery(status, tenantId, partnerId, from, to, search);
        var total = await query.CountAsync(cancellationToken);

        var items = await ProjectAsync(
            query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize),
            cancellationToken);

        return Ok(new AdminPagedResult<AdminOrderListItemDto>(items, total, page, pageSize));
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? status,
        [FromQuery] Guid? tenantId,
        [FromQuery] Guid? partnerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(status, tenantId, partnerId, from, to, search);
        var rows = await ProjectAsync(
            query.OrderByDescending(x => x.CreatedAt).Take(MaxExportRows),
            cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("order_id,created_at,partner,buyer,status,delivery_type,delivery_status,payment_status,subtotal_delivery_fee,total,currency");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",",
                row.Id,
                row.CreatedAt.UtcDateTime.ToString("O"),
                EscapeCsv(row.PartnerName),
                EscapeCsv(row.BuyerName),
                EscapeCsv(row.Status),
                EscapeCsv(row.DeliveryType),
                EscapeCsv(row.DeliveryStatus),
                EscapeCsv(row.PaymentStatus),
                row.DeliveryFee,
                row.TotalAmount,
                EscapeCsv(row.Currency)));
        }

        var fileName = $"orders_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminOrderDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var order = await _db.Orders
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return NotFound();
        }

        var partnerName = await _db.Partners
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.Id == order.PartnerId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "(sin partner)";

        string? courierName = null;
        if (order.CourierId.HasValue)
        {
            courierName = await _db.Couriers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.Id == order.CourierId.Value)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var productIds = order.Items.Select(x => x.ProductId).Distinct().ToList();
        var productNames = await _db.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var payment = await _db.Payments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrderId == order.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new AdminOrderPaymentDto(
                x.Id,
                x.Provider,
                x.Status,
                x.StatusDetail,
                x.Amount,
                x.PaidAmount,
                x.MercadoPagoPaymentId,
                x.PaymentMethod,
                x.DateApproved,
                x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        var settlement = await _db.DeliverySettlements
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrderId == order.Id)
            .Select(x => new AdminOrderSettlementDto(
                x.Id,
                x.Status,
                x.GrossAmount,
                x.PlatformFeeAmount,
                x.MercadoPagoFeeAmount,
                x.NetToCourierAmount,
                x.SettledAt))
            .FirstOrDefaultAsync(cancellationToken);

        var items = order.Items
            .OrderBy(x => x.Id)
            .Select(x => new AdminOrderItemDto(
                x.ProductId,
                productNames.TryGetValue(x.ProductId, out var name) ? name : "(producto eliminado)",
                x.Quantity,
                x.UnitPrice,
                x.TotalPrice))
            .ToList();

        return Ok(new AdminOrderDetailDto(
            order.Id,
            order.TenantId,
            order.PartnerId,
            partnerName,
            order.BuyerName,
            order.BuyerEmail,
            order.Status,
            order.Subtotal,
            order.DeliveryFee,
            order.TotalAmount,
            order.PlatformFeeAmount,
            order.NetAmount,
            order.Currency,
            order.DeliveryType,
            order.DeliveryStatus,
            order.DeliveryAddress,
            order.DeliveryProviderName,
            order.CourierId,
            courierName,
            order.CreatedAt,
            order.UpdatedAt,
            items,
            payment,
            settlement));
    }

    private IQueryable<Persistence.Entities.Order> BuildQuery(
        string? status,
        Guid? tenantId,
        Guid? partnerId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? search)
    {
        var query = _db.Orders.IgnoreQueryFilters().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status.ToLower() == normalized);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        if (partnerId.HasValue)
        {
            query = query.Where(x => x.PartnerId == partnerId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ExternalReference != null && x.ExternalReference.ToLower().Contains(term)) ||
                (x.BuyerEmail != null && x.BuyerEmail.ToLower().Contains(term)) ||
                (x.BuyerName != null && x.BuyerName.ToLower().Contains(term)) ||
                x.Id.ToString().ToLower().Contains(term));
        }

        return query;
    }

    private async Task<IReadOnlyList<AdminOrderListItemDto>> ProjectAsync(
        IQueryable<Persistence.Entities.Order> query,
        CancellationToken cancellationToken)
    {
        var orders = await query
            .Select(x => new
            {
                x.Id,
                x.TenantId,
                x.PartnerId,
                x.BuyerName,
                x.BuyerEmail,
                x.Status,
                x.TotalAmount,
                x.DeliveryFee,
                x.Currency,
                x.DeliveryType,
                x.DeliveryStatus,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return Array.Empty<AdminOrderListItemDto>();
        }

        var partnerIds = orders.Select(x => x.PartnerId).Distinct().ToList();
        var partnerNames = await _db.Partners
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => partnerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var orderIds = orders.Select(x => x.Id).ToList();
        var paymentStatuses = await _db.Payments
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.OrderId != null && orderIds.Contains(x.OrderId.Value))
            .GroupBy(x => x.OrderId!.Value)
            .Select(g => new
            {
                OrderId = g.Key,
                Status = g.OrderByDescending(x => x.CreatedAt).Select(x => x.Status).First()
            })
            .ToDictionaryAsync(x => x.OrderId, x => x.Status, cancellationToken);

        return orders
            .Select(x => new AdminOrderListItemDto(
                x.Id,
                x.TenantId,
                x.PartnerId,
                partnerNames.TryGetValue(x.PartnerId, out var partnerName) ? partnerName : "(sin partner)",
                x.BuyerName,
                x.BuyerEmail,
                x.Status,
                x.TotalAmount,
                x.DeliveryFee,
                x.Currency,
                x.DeliveryType,
                x.DeliveryStatus,
                paymentStatuses.TryGetValue(x.Id, out var paymentStatus) ? paymentStatus : null,
                x.CreatedAt))
            .ToList();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
