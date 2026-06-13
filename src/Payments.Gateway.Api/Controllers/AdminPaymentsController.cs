using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payments.Gateway.Api.Contracts.Admin;
using Payments.Gateway.Api.Persistence;
using Payments.Gateway.Api.Persistence.Entities;
using Payments.Gateway.Api.Services;

namespace Payments.Gateway.Api.Controllers;

[ApiController]
[Authorize(Policy = "payments.viewer")]
[Route("v1/admin")]
public sealed class AdminPaymentsController : ControllerBase
{
    private const int StuckPendingHours = 24;

    private readonly PaymentsDbContext _db;
    private readonly IComunaClicNotifier _notifier;

    public AdminPaymentsController(PaymentsDbContext db, IComunaClicNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdminDashboardSummaryDto>> Dashboard(CancellationToken cancellationToken)
    {
        var intents = await _db.PaymentIntents
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var providers = intents
            .GroupBy(x => x.Provider)
            .Select(g => new AdminProviderBreakdownDto(
                g.Key,
                g.Count(),
                g.Where(x => IsCaptured(x.Status)).Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.Provider)
            .ToList();

        var activeSubscriptions = await _db.Subscriptions
            .AsNoTracking()
            .CountAsync(x => x.Status == "active", cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var startOfWeek = startOfDay.AddDays(-(((int)startOfDay.DayOfWeek + 6) % 7));
        var startOfMonth = new DateTimeOffset(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc));

        var captured = intents.Where(x => IsCaptured(x.Status)).ToList();

        return Ok(new AdminDashboardSummaryDto(
            captured.Sum(x => x.Amount),
            captured.Count,
            intents.Count(x => string.Equals(x.Status, "pending", StringComparison.OrdinalIgnoreCase)),
            intents.Count(x => IsFailed(x.Status)),
            activeSubscriptions,
            providers,
            captured.Where(x => x.CreatedAt >= startOfDay).Sum(x => x.Amount),
            captured.Where(x => x.CreatedAt >= startOfWeek).Sum(x => x.Amount),
            captured.Where(x => x.CreatedAt >= startOfMonth).Sum(x => x.Amount),
            await ComputeAlertsAsync(cancellationToken)));
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<AdminAlertsDto>> Alerts(CancellationToken cancellationToken)
        => Ok(await ComputeAlertsAsync(cancellationToken));

    [HttpGet("payment-intents")]
    public async Task<ActionResult<IReadOnlyList<AdminPaymentIntentListItemDto>>> ListPaymentIntents(
        [FromQuery] string? provider,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        var items = await BuildIntentsQuery(provider, status, search, from, to)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AdminPaymentIntentListItemDto(
                x.Id,
                x.ExternalReference,
                x.Amount,
                x.Currency,
                x.Status,
                x.Provider,
                x.ProviderToken,
                x.AuthorizationCode,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("payment-intents/export.csv")]
    public async Task<IActionResult> ExportPaymentIntentsCsv(
        [FromQuery] string? provider,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var rows = await BuildIntentsQuery(provider, status, search, from, to)
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("intent_id,created_at,external_reference,provider,status,amount,currency,authorization_code,review_status,core_notified_at");
        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(",",
                row.Id,
                row.CreatedAt.UtcDateTime.ToString("O"),
                EscapeCsv(row.ExternalReference),
                EscapeCsv(row.Provider),
                EscapeCsv(row.Status),
                row.Amount,
                EscapeCsv(row.Currency),
                EscapeCsv(row.AuthorizationCode),
                EscapeCsv(row.ReviewStatus),
                row.CoreNotifiedAt?.UtcDateTime.ToString("O") ?? string.Empty));
        }

        var fileName = $"payment_intents_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("payment-intents/{id:guid}")]
    public async Task<ActionResult<AdminPaymentIntentDetailDto>> GetPaymentIntent(Guid id, CancellationToken cancellationToken)
    {
        var intent = await _db.PaymentIntents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (intent is null)
        {
            return NotFound();
        }

        var events = await _db.ProviderEvents
            .AsNoTracking()
            .Where(x => x.IntentId == id)
            .OrderByDescending(x => x.ReceivedAt)
            .Select(x => new AdminProviderEventDto(
                x.Id,
                x.ProviderEventId,
                x.EventType,
                x.Payload,
                x.ReceivedAt))
            .ToListAsync(cancellationToken);

        return Ok(new AdminPaymentIntentDetailDto(
            intent.Id,
            intent.ExternalReference,
            intent.Amount,
            intent.Currency,
            intent.Status,
            intent.Provider,
            intent.ProviderToken,
            intent.AuthorizationCode,
            intent.RawResponse,
            intent.CreatedAt,
            intent.UpdatedAt,
            events,
            intent.ReviewStatus,
            intent.ReviewNote,
            intent.CoreNotifiedAt,
            intent.CoreNotifyAttempts));
    }

    [HttpPost("payment-intents/{id:guid}/notify-core")]
    [Authorize(Policy = "payments.operator")]
    public async Task<IActionResult> NotifyCore(Guid id, CancellationToken cancellationToken)
    {
        var intent = await _db.PaymentIntents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (intent is null)
        {
            return NotFound();
        }

        var providerEventId = $"core.notify.retry:{intent.Id}:{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        await _notifier.NotifyPaymentAsync(intent, intent.Status, providerEventId, intent.RawResponse, cancellationToken);

        intent.CoreNotifiedAt = DateTimeOffset.UtcNow;
        intent.CoreNotifyAttempts += 1;

        // Evento sintético para que el reintento quede visible en el timeline del intent.
        _db.ProviderEvents.Add(new ProviderEvent
        {
            ProviderEventId = providerEventId,
            IntentId = intent.Id,
            EventType = "core.notify.retry",
            Payload = "{\"source\":\"admin\",\"action\":\"notify-core\"}",
            ReceivedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Accepted(new { intent.Id, intent.CoreNotifiedAt, intent.CoreNotifyAttempts });
    }

    [HttpPost("payment-intents/{id:guid}/review")]
    [Authorize(Policy = "payments.operator")]
    public async Task<IActionResult> Review(Guid id, [FromBody] AdminReviewRequest request, CancellationToken cancellationToken)
    {
        var normalized = request.Status?.Trim().ToLowerInvariant();
        if (normalized is not ("flagged" or "resolved"))
        {
            return BadRequest(new { message = "Status debe ser 'flagged' o 'resolved'." });
        }

        var intent = await _db.PaymentIntents.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (intent is null)
        {
            return NotFound();
        }

        intent.ReviewStatus = normalized;
        intent.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { intent.Id, intent.ReviewStatus, intent.ReviewNote });
    }

    [HttpGet("reconciliation")]
    public async Task<ActionResult<IReadOnlyList<AdminReconciliationRowDto>>> Reconciliation(CancellationToken cancellationToken)
    {
        var threshold = DateTimeOffset.UtcNow.AddHours(-StuckPendingHours);
        var rows = new List<AdminReconciliationRowDto>();

        var stuckIntents = await _db.PaymentIntents
            .AsNoTracking()
            .Where(x => x.Status == "pending" && x.CreatedAt < threshold)
            .OrderBy(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        rows.AddRange(stuckIntents.Select(x => new AdminReconciliationRowDto(
            "stuck_pending",
            x.Id,
            x.ExternalReference,
            x.Provider,
            x.Status,
            x.Amount,
            x.Currency,
            $"Intent pendiente hace más de {StuckPendingHours} horas sin resolución del proveedor.",
            x.CreatedAt)));

        var intentsWithoutEvents = await _db.PaymentIntents
            .AsNoTracking()
            .Where(x => x.Status != "pending" && !_db.ProviderEvents.Any(e => e.IntentId == x.Id))
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        rows.AddRange(intentsWithoutEvents.Select(x => new AdminReconciliationRowDto(
            "intent_without_events",
            x.Id,
            x.ExternalReference,
            x.Provider,
            x.Status,
            x.Amount,
            x.Currency,
            "Intent con estado final pero sin eventos del proveedor registrados.",
            x.CreatedAt)));

        var chargesWithoutAuth = await _db.Charges
            .AsNoTracking()
            .Include(x => x.Intent)
            .Where(x => x.Status == "approved" && x.AuthorizationCode == null)
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        rows.AddRange(chargesWithoutAuth.Select(x => new AdminReconciliationRowDto(
            "charge_without_auth",
            x.IntentId,
            x.Intent != null ? x.Intent.ExternalReference : null,
            x.Intent != null ? x.Intent.Provider : null,
            x.Status,
            x.Amount,
            x.Currency,
            "Cargo aprobado sin código de autorización del proveedor.",
            x.CreatedAt)));

        var flagged = await _db.PaymentIntents
            .AsNoTracking()
            .Where(x => x.ReviewStatus == "flagged")
            .OrderByDescending(x => x.UpdatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        rows.AddRange(flagged.Select(x => new AdminReconciliationRowDto(
            "flagged",
            x.Id,
            x.ExternalReference,
            x.Provider,
            x.Status,
            x.Amount,
            x.Currency,
            string.IsNullOrWhiteSpace(x.ReviewNote) ? "Marcado para revisión manual." : x.ReviewNote!,
            x.UpdatedAt)));

        return Ok(rows.OrderByDescending(x => x.OccurredAt).ToList());
    }

    private IQueryable<PaymentIntent> BuildIntentsQuery(
        string? provider,
        string? status,
        string? search,
        DateTimeOffset? from,
        DateTimeOffset? to)
    {
        var query = _db.PaymentIntents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(x => x.Provider == provider.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.Trim());
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
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.ExternalReference, $"%{term}%") ||
                (x.ProviderToken != null && EF.Functions.ILike(x.ProviderToken, $"%{term}%")));
        }

        return query;
    }

    private async Task<AdminAlertsDto> ComputeAlertsAsync(CancellationToken cancellationToken)
    {
        var stuckThreshold = DateTimeOffset.UtcNow.AddHours(-StuckPendingHours);
        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);

        var stuckPending = await _db.PaymentIntents
            .AsNoTracking()
            .CountAsync(x => x.Status == "pending" && x.CreatedAt < stuckThreshold, cancellationToken);

        var flagged = await _db.PaymentIntents
            .AsNoTracking()
            .CountAsync(x => x.ReviewStatus == "flagged", cancellationToken);

        var failedLast7Days = await _db.PaymentIntents
            .AsNoTracking()
            .CountAsync(x => (x.Status == "failed" || x.Status == "rejected" || x.Status == "canceled" || x.Status == "cancelled")
                && x.CreatedAt >= weekAgo, cancellationToken);

        var chargesWithoutAuth = await _db.Charges
            .AsNoTracking()
            .CountAsync(x => x.Status == "approved" && x.AuthorizationCode == null, cancellationToken);

        var coreNotifyFailing = await _db.PaymentIntents
            .AsNoTracking()
            .CountAsync(x => x.CoreNotifyAttempts > 0 && x.CoreNotifiedAt == null, cancellationToken);

        return new AdminAlertsDto(stuckPending, flagged, failedLast7Days, chargesWithoutAuth, coreNotifyFailing);
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

    private static bool IsCaptured(string status)
        => string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);

    private static bool IsFailed(string status)
        => string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "cancelled", StringComparison.OrdinalIgnoreCase);
}
