using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ServiceStaffSync
{
    public static async Task<IReadOnlyList<Guid>> LoadProfessionalIdsAsync(CoreDbContext db, Guid serviceId, CancellationToken cancellationToken)
        => await db.ServiceProfessionals.AsNoTracking()
            .Where(x => x.ServiceId == serviceId)
            .Select(x => x.ProfessionalId)
            .ToListAsync(cancellationToken);

    public static async Task EnrichAsync(CoreDbContext db, Service service, CancellationToken cancellationToken)
    {
        service.ProfessionalIds = await LoadProfessionalIdsAsync(db, service.Id, cancellationToken);
    }

    public static async Task EnrichManyAsync(CoreDbContext db, IList<Service> services, CancellationToken cancellationToken)
    {
        if (services.Count == 0)
        {
            return;
        }

        var ids = services.Select(x => x.Id).ToList();
        var links = await db.ServiceProfessionals.AsNoTracking()
            .Where(x => ids.Contains(x.ServiceId))
            .ToListAsync(cancellationToken);

        var byService = links.GroupBy(x => x.ServiceId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.ProfessionalId).ToList());
        foreach (var service in services)
        {
            service.ProfessionalIds = byService.TryGetValue(service.Id, out var profIds)
                ? profIds
                : Array.Empty<Guid>();
        }
    }

    public static async Task ReplaceAsync(
        CoreDbContext db,
        Service service,
        IReadOnlyList<Guid>? professionalIds,
        CancellationToken cancellationToken)
    {
        var existing = await db.ServiceProfessionals
            .Where(x => x.ServiceId == service.Id)
            .ToListAsync(cancellationToken);
        db.ServiceProfessionals.RemoveRange(existing);

        if (!service.IsBookable || professionalIds is null || professionalIds.Count == 0)
        {
            service.ProfessionalIds = Array.Empty<Guid>();
            return;
        }

        var distinct = professionalIds.Where(x => x != Guid.Empty).Distinct().ToList();
        var valid = await db.Professionals.AsNoTracking()
            .Where(x =>
                distinct.Contains(x.Id)
                && x.TenantId == service.TenantId
                && x.IsActive
                && (x.PartnerId == service.PartnerId || x.PartnerId == null))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var professionalId in valid)
        {
            db.ServiceProfessionals.Add(new ServiceProfessional
            {
                ServiceId = service.Id,
                ProfessionalId = professionalId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        service.ProfessionalIds = valid;
    }

    public static async Task<string?> ValidateBookableStaffAsync(
        CoreDbContext db,
        Guid partnerId,
        Guid tenantId,
        bool isBookable,
        IReadOnlyList<Guid>? professionalIds,
        CancellationToken cancellationToken)
    {
        if (!isBookable)
        {
            return null;
        }

        var activeOnTeam = await db.Professionals.AsNoTracking()
            .CountAsync(
                x => x.TenantId == tenantId
                     && x.PartnerId == partnerId
                     && x.IsActive,
                cancellationToken);

        if (activeOnTeam == 0)
        {
            return "Registrá al menos un profesional en tu equipo antes de publicar avisos reservables.";
        }

        var assigned = professionalIds?.Where(x => x != Guid.Empty).Distinct().Count() ?? 0;
        if (assigned < 1)
        {
            return "Indicá qué profesional(es) atienden este servicio para habilitar turnos en paralelo a la misma hora.";
        }

        return null;
    }
}
