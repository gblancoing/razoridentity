using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Customers;

public interface IGuestCustomerService
{
    Task<Customer?> EnsureGuestCustomerAsync(
        Guid tenantId,
        GuestContactRequest guest,
        string? deliveryAddress,
        CancellationToken cancellationToken = default);

    string? ValidateGuestContact(GuestContactRequest guest);
}

public sealed class GuestCustomerService : IGuestCustomerService
{
    private readonly CoreDbContext _db;

    public GuestCustomerService(CoreDbContext db)
    {
        _db = db;
    }

    public async Task<Customer?> EnsureGuestCustomerAsync(
        Guid tenantId,
        GuestContactRequest guest,
        string? deliveryAddress,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = guest.Email.Trim().ToLowerInvariant();
        var customer = await _db.Customers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail,
                cancellationToken);

        if (customer is null)
        {
            customer = new Customer
            {
                TenantId = tenantId,
                Email = normalizedEmail,
                FullName = guest.FullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(guest.Phone) ? null : guest.Phone.Trim(),
                Address = string.IsNullOrWhiteSpace(deliveryAddress) ? null : deliveryAddress.Trim(),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _db.Customers.Add(customer);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                _db.Entry(customer).State = EntityState.Detached;
                customer = await _db.Customers
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(
                        x => x.TenantId == tenantId && x.Email != null && x.Email.ToLower() == normalizedEmail,
                        cancellationToken);
            }
        }
        else
        {
            var updated = false;
            if (!string.IsNullOrWhiteSpace(guest.FullName))
            {
                customer.FullName = guest.FullName.Trim();
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(guest.Phone))
            {
                customer.Phone = guest.Phone.Trim();
                updated = true;
            }

            if (!string.IsNullOrWhiteSpace(deliveryAddress))
            {
                customer.Address = deliveryAddress.Trim();
                updated = true;
            }

            if (updated)
            {
                customer.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        return customer;
    }

    public string? ValidateGuestContact(GuestContactRequest guest)
    {
        if (string.IsNullOrWhiteSpace(guest.FullName) || guest.FullName.Trim().Length < 2)
        {
            return "Full name is required.";
        }

        if (string.IsNullOrWhiteSpace(guest.Email) || !guest.Email.Contains('@'))
        {
            return "A valid email is required.";
        }

        return null;
    }
}
