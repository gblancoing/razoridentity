using System.Text.Json;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Admin;

public sealed class SiteContentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CoreDbContext _db;

    public SiteContentService(CoreDbContext db)
    {
        _db = db;
    }

    public async Task<SiteContentDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var home = await GetSectionAsync("home", DefaultContent.Home, cancellationToken);
        var footer = await GetSectionAsync("footer", DefaultContent.Footer, cancellationToken);
        return new SiteContentDto(home, footer);
    }

    public async Task<SiteContentDto> SaveAsync(SiteContentUpdateRequest request, CancellationToken cancellationToken = default)
    {
        await UpsertSectionAsync("home", request.Home, cancellationToken);
        await UpsertSectionAsync("footer", request.Footer, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private async Task<T> GetSectionAsync<T>(string section, T fallback, CancellationToken cancellationToken)
    {
        var entity = await _db.SiteContentSettings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Section == section, cancellationToken);

        if (entity is null || string.IsNullOrWhiteSpace(entity.ContentJson))
        {
            return fallback;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(entity.ContentJson, JsonOptions) ?? fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private async Task UpsertSectionAsync<T>(string section, T payload, CancellationToken cancellationToken)
    {
        var entity = await _db.SiteContentSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Section == section, cancellationToken);

        if (entity is null)
        {
            entity = new SiteContentSetting
            {
                Section = section
            };
            _db.SiteContentSettings.Add(entity);
        }

        entity.ContentJson = JsonSerializer.Serialize(payload, JsonOptions);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static class DefaultContent
    {
        public static readonly HomeContentDto Home = new(
            "Conecta con tu barrio y compra local",
            "Encuentra negocios, servicios y profesionales cerca de ti en una sola plataforma.",
            "https://images.unsplash.com/photo-1488459716781-31db52582fe9?auto=format&fit=crop&w=1600&q=80",
            "Explorar categorías",
            "/categorias",
            "Registrar negocio",
            "/register?intent=partner");

        public static readonly FooterContentDto Footer = new(
            "© 2026 ComunaClic. Impulsando el comercio local.",
            "https://www.instagram.com",
            "https://www.facebook.com",
            "https://www.linkedin.com");
    }
}
