using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Payments.Gateway.Api.Contracts.Admin;
using Payments.Gateway.Api.Persistence;

namespace Payments.Gateway.Api.Controllers;

[ApiController]
[Authorize(Policy = "payments.viewer")]
[Route("v1/admin/subscriptions")]
public sealed class AdminSubscriptionsController : ControllerBase
{
    private readonly PaymentsDbContext _db;

    public AdminSubscriptionsController(PaymentsDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminSubscriptionDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 500);

        var query = _db.Subscriptions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == normalized);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.ExternalReference, $"%{term}%") ||
                EF.Functions.ILike(x.CustomerId, $"%{term}%") ||
                (x.PlanName != null && EF.Functions.ILike(x.PlanName, $"%{term}%")));
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AdminSubscriptionDto(
                x.Id,
                x.CustomerId,
                x.ExternalReference,
                x.PlanName,
                x.Provider,
                x.Amount,
                x.Currency,
                x.BillingInterval,
                x.Status,
                x.NextChargeAt,
                x.Attempts.OrderByDescending(a => a.AttemptedAt).Select(a => (DateTimeOffset?)a.AttemptedAt).FirstOrDefault(),
                x.Attempts.OrderByDescending(a => a.AttemptedAt).Select(a => a.Status).FirstOrDefault(),
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminSubscriptionDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await _db.Subscriptions
            .AsNoTracking()
            .Include(x => x.Attempts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (subscription is null)
        {
            return NotFound();
        }

        return Ok(new AdminSubscriptionDetailDto(
            subscription.Id,
            subscription.CustomerId,
            subscription.CustomerTokenId,
            subscription.ExternalReference,
            subscription.PlanName,
            subscription.Provider,
            subscription.Amount,
            subscription.Currency,
            subscription.BillingInterval,
            subscription.Status,
            subscription.NextChargeAt,
            subscription.CancelledAt,
            subscription.CreatedAt,
            subscription.UpdatedAt,
            subscription.Attempts
                .OrderByDescending(x => x.AttemptedAt)
                .Select(x => new AdminSubscriptionAttemptDto(
                    x.Id,
                    x.IntentId,
                    x.ChargeId,
                    x.Status,
                    x.ErrorMessage,
                    x.AttemptedAt))
                .ToList()));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "payments.operator")]
    public Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        => ChangeStatus(id, "cancelled", cancellationToken);

    [HttpPost("{id:guid}/pause")]
    [Authorize(Policy = "payments.operator")]
    public Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
        => ChangeStatus(id, "paused", cancellationToken);

    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = "payments.operator")]
    public Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
        => ChangeStatus(id, "active", cancellationToken);

    private async Task<IActionResult> ChangeStatus(Guid id, string status, CancellationToken cancellationToken)
    {
        var subscription = await _db.Subscriptions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (subscription is null)
        {
            return NotFound();
        }

        if (subscription.Status == "cancelled" && status != "cancelled")
        {
            return Conflict(new { message = "La suscripción ya fue cancelada y no puede reactivarse." });
        }

        subscription.Status = status;
        subscription.UpdatedAt = DateTimeOffset.UtcNow;
        subscription.CancelledAt = status == "cancelled" ? DateTimeOffset.UtcNow : subscription.CancelledAt;
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { subscription.Id, subscription.Status, subscription.CancelledAt });
    }
}
