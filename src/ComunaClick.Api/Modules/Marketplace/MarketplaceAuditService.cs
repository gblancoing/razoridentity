using System.Text.Json;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class MarketplaceAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CoreDbContext _db;

    public MarketplaceAuditService(CoreDbContext db)
    {
        _db = db;
    }

    public Task WriteAsync(
        string actor,
        string action,
        string entityType,
        string entityId,
        object? data,
        CancellationToken cancellationToken = default)
    {
        var log = new AuditLog
        {
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DataJson = JsonSerializer.Serialize(data ?? new { }, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.AuditLogs.Add(log);
        return _db.SaveChangesAsync(cancellationToken);
    }
}
