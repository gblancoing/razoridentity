using ComunaClick.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Modules.Catalog;

/// <summary>
/// Reglas de jerarquía de las categorías propias del local (máximo dos niveles:
/// categoría → subcategoría). Extraídas del controller para poder testearlas.
/// </summary>
public static class PartnerCatalogCategoryRules
{
    public static async Task<(bool Ok, string? Message)> ValidateParentAsync(
        CoreDbContext db,
        Guid partnerId,
        Guid parentId,
        Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (categoryId.HasValue && parentId == categoryId.Value)
        {
            return (false, "Una categoría no puede ser su propia subcategoría.");
        }

        var parent = await db.PartnerCatalogCategories.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parentId && x.PartnerId == partnerId, cancellationToken);
        if (parent is null)
        {
            return (false, "La categoría padre no existe.");
        }

        if (parent.ParentId is not null)
        {
            return (false, "Solo se permiten dos niveles: una subcategoría no puede tener subcategorías.");
        }

        if (categoryId.HasValue)
        {
            var hasChildren = await db.PartnerCatalogCategories.AsNoTracking()
                .AnyAsync(x => x.ParentId == categoryId.Value, cancellationToken);
            if (hasChildren)
            {
                return (false, "Esta categoría tiene subcategorías: no puede convertirse en subcategoría.");
            }
        }

        return (true, null);
    }

    public static async Task<(bool Ok, string? Message)> CanDeleteAsync(
        CoreDbContext db,
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var hasChildren = await db.PartnerCatalogCategories.AsNoTracking()
            .AnyAsync(x => x.ParentId == categoryId, cancellationToken);
        if (hasChildren)
        {
            return (false, "No se puede eliminar: la categoría tiene subcategorías. Eliminá o reasigná las subcategorías primero.");
        }

        var hasProducts = await db.Products.AsNoTracking()
            .AnyAsync(x => x.PartnerCatalogCategoryId == categoryId, cancellationToken);
        if (hasProducts)
        {
            return (false, "No se puede eliminar: hay productos en esta sección. Reasignalos primero.");
        }

        return (true, null);
    }
}
