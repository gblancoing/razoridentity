using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payments.Gateway.Api.Contracts.Admin;
using Payments.Gateway.Api.Persistence;

namespace Payments.Gateway.Api.Controllers;

[ApiController]
[Authorize(Policy = "payments.admin")]
[Route("v1/admin")]
public sealed class AdminPaymentsController : ControllerBase
{
    private readonly PaymentsDbContext _db;

    public AdminPaymentsController(PaymentsDbContext db)
    {
        _db = db;
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
                g.Where(x => string.Equals(x.Status, "captured", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)))
            .OrderByDescending(x => x.Amount)
            .ThenBy(x => x.Provider)
            .ToList();

        var subscriptionCandidates = intents
            .Where(x => IsSubscriptionReference(x.ExternalReference))
            .Select(x => x.ExternalReference)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return Ok(new AdminDashboardSummaryDto(
            intents.Where(x => string.Equals(x.Status, "captured", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount),
            intents.Count(x => string.Equals(x.Status, "captured", StringComparison.OrdinalIgnoreCase)),
            intents.Count(x => string.Equals(x.Status, "pending", StringComparison.OrdinalIgnoreCase)),
            intents.Count(x => string.Equals(x.Status, "failed", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Status, "canceled", StringComparison.OrdinalIgnoreCase)),
            subscriptionCandidates,
            providers));
    }

    [HttpGet("payment-intents")]
    public async Task<ActionResult<IReadOnlyList<AdminPaymentIntentListItemDto>>> ListPaymentIntents(
        [FromQuery] string? provider,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        var query = _db.PaymentIntents
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(provider))
        {
            query = query.Where(x => x.Provider == provider.Trim());
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.Trim());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.ExternalReference, $"%{term}%") ||
                (x.ProviderToken != null && EF.Functions.ILike(x.ProviderToken, $"%{term}%")));
        }

        var items = await query
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
            events));
    }

    [HttpGet("subscriptions")]
    public async Task<ActionResult<IReadOnlyList<AdminSubscriptionCandidateDto>>> ListSubscriptions(CancellationToken cancellationToken)
    {
        var intents = await _db.PaymentIntents
            .AsNoTracking()
            .Where(x => EF.Functions.ILike(x.ExternalReference, "%SUBS%"))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var subscriptions = intents
            .GroupBy(x => x.ExternalReference, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.CreatedAt).First();
                return new AdminSubscriptionCandidateDto(
                    latest.ExternalReference,
                    BuildPlanName(latest.ExternalReference),
                    latest.Provider,
                    MapSubscriptionStatus(latest.Status),
                    latest.Amount,
                    latest.Currency,
                    latest.CreatedAt,
                    latest.CreatedAt.AddMonths(1));
            })
            .OrderByDescending(x => x.LastPaymentAt)
            .ToList();

        return Ok(subscriptions);
    }

    private static bool IsSubscriptionReference(string? externalReference)
        => !string.IsNullOrWhiteSpace(externalReference)
           && externalReference.Contains("SUBS", StringComparison.OrdinalIgnoreCase);

    private static string BuildPlanName(string externalReference)
    {
        if (string.IsNullOrWhiteSpace(externalReference))
        {
            return "Plan recurrente";
        }

        var normalized = externalReference
            .Replace('_', '-')
            .Split('-', StringSplitOptions.RemoveEmptyEntries);

        return string.Join(" ", normalized
            .Where(x => !string.Equals(x, "SUBS", StringComparison.OrdinalIgnoreCase))
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant()));
    }

    private static string MapSubscriptionStatus(string paymentStatus)
        => paymentStatus.ToLowerInvariant() switch
        {
            "captured" => "active",
            "pending" => "trial",
            "failed" => "past_due",
            "canceled" => "canceled",
            _ => "unknown"
        };
}
