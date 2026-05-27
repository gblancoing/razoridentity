using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

internal static class ServiceMediaSync
{
  public static async Task EnrichAsync(CoreDbContext db, Service service, CancellationToken cancellationToken)
  {
    var images = await db.ServiceImages.AsNoTracking()
      .Where(x => x.ServiceId == service.Id)
      .OrderBy(x => x.SortOrder)
      .ThenBy(x => x.CreatedAt)
      .ToListAsync(cancellationToken);

    service.ImageUrls = images.Select(x => x.Url).ToList();
    service.ImageIds = images.Select(x => x.Id).ToList();
    service.Images = images
      .Select(x => new ServiceImageSnapshot { Id = x.Id, Url = x.Url, SortOrder = x.SortOrder })
      .ToList();

    if (string.IsNullOrWhiteSpace(service.ImageUrl) && service.ImageUrls.Count > 0)
    {
      service.ImageUrl = service.ImageUrls[0];
    }
  }

  public static async Task EnrichManyAsync(CoreDbContext db, IList<Service> services, CancellationToken cancellationToken)
  {
    if (services.Count == 0)
    {
      return;
    }

    var ids = services.Select(x => x.Id).ToList();
    var images = await db.ServiceImages.AsNoTracking()
      .Where(x => ids.Contains(x.ServiceId))
      .OrderBy(x => x.SortOrder)
      .ThenBy(x => x.CreatedAt)
      .ToListAsync(cancellationToken);

    var byService = images.GroupBy(x => x.ServiceId).ToDictionary(g => g.Key, g => g.ToList());
    foreach (var service in services)
    {
      if (!byService.TryGetValue(service.Id, out var list))
      {
        service.ImageUrls = Array.Empty<string>();
        service.ImageIds = Array.Empty<Guid>();
        service.Images = Array.Empty<ServiceImageSnapshot>();
        continue;
      }

      service.ImageUrls = list.Select(x => x.Url).ToList();
      service.ImageIds = list.Select(x => x.Id).ToList();
      service.Images = list
        .Select(x => new ServiceImageSnapshot { Id = x.Id, Url = x.Url, SortOrder = x.SortOrder })
        .ToList();
      if (string.IsNullOrWhiteSpace(service.ImageUrl) && service.ImageUrls.Count > 0)
      {
        service.ImageUrl = service.ImageUrls[0];
      }
    }
  }

  public static async Task SyncPrimaryImageUrlAsync(CoreDbContext db, Guid serviceId, CancellationToken cancellationToken)
  {
    var service = await db.Services.FirstOrDefaultAsync(x => x.Id == serviceId, cancellationToken);
    if (service is null)
    {
      return;
    }

    var first = await db.ServiceImages.AsNoTracking()
      .Where(x => x.ServiceId == serviceId)
      .OrderBy(x => x.SortOrder)
      .ThenBy(x => x.CreatedAt)
      .Select(x => x.Url)
      .FirstOrDefaultAsync(cancellationToken);

    service.ImageUrl = first;
    service.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
  }
}
