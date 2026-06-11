using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Crm;

public interface ICustomerLinkService
{
    Task<(Customer? Customer, string? Error)> LinkGuestCustomerAsync(
        Guid guestCustomerId,
        Guid tenantId,
        string authenticatedEmail,
        string? authenticatedName,
        CancellationToken cancellationToken = default);
}

public sealed class CustomerLinkService : ICustomerLinkService
{
    private readonly CoreDbContext _db;

    public CustomerLinkService(CoreDbContext db)
    {
        _db = db;
    }

    public async Task<(Customer? Customer, string? Error)> LinkGuestCustomerAsync(
        Guid guestCustomerId,
        Guid tenantId,
        string authenticatedEmail,
        string? authenticatedName,
        CancellationToken cancellationToken = default)
    {
        if (guestCustomerId == Guid.Empty || tenantId == Guid.Empty)
        {
            return (null, "CustomerId and TenantId are required.");
        }

        if (string.IsNullOrWhiteSpace(authenticatedEmail))
        {
            return (null, "Authenticated email is required.");
        }

        var normalizedEmail = authenticatedEmail.Trim().ToLowerInvariant();
        var guest = await _db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == guestCustomerId && x.TenantId == tenantId, cancellationToken);

        if (guest is null)
        {
            return (null, "Guest customer profile was not found.");
        }

        var guestEmail = guest.Email?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(guestEmail) &&
            !string.Equals(guestEmail, normalizedEmail, StringComparison.Ordinal))
        {
            return (null, "Guest profile email does not match your account email.");
        }

        var canonical = await _db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (canonical is null)
        {
            guest.Email = normalizedEmail;
            if (!string.IsNullOrWhiteSpace(authenticatedName))
            {
                guest.FullName = authenticatedName.Trim();
            }

            guest.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return (guest, null);
        }

        if (canonical.Id == guest.Id)
        {
            if (!string.IsNullOrWhiteSpace(authenticatedName) && string.IsNullOrWhiteSpace(canonical.FullName))
            {
                canonical.FullName = authenticatedName.Trim();
                canonical.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return (canonical, null);
        }

        await ReassignCustomerReferencesAsync(guest.Id, canonical.Id, cancellationToken);
        _db.Customers.Remove(guest);
        await _db.SaveChangesAsync(cancellationToken);

        return (canonical, null);
    }

    private async Task ReassignCustomerReferencesAsync(
        Guid fromCustomerId,
        Guid toCustomerId,
        CancellationToken cancellationToken)
    {
        await _db.Orders
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), cancellationToken);

        await _db.Bookings
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), cancellationToken);

        await _db.ShoppingCarts
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), cancellationToken);

        await _db.Leads
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), cancellationToken);

        await _db.Interactions
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), cancellationToken);

        await _db.BuyerFavorites
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), cancellationToken);

        await _db.InboxThreads
            .IgnoreQueryFilters()
            .Where(x => x.CustomerId == fromCustomerId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CustomerId, toCustomerId), cancellationToken);
    }
}
