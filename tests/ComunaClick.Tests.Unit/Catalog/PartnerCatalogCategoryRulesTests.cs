using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Xunit;

namespace ComunaClick.Tests.Unit.Catalog;

public sealed class PartnerCatalogCategoryRulesTests
{
    private static PartnerCatalogCategory AddCategory(CoreDbContext db, Guid tenantId, Guid partnerId, string name, Guid? parentId = null)
    {
        var category = new PartnerCatalogCategory
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partnerId,
            Name = name,
            ParentId = parentId,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.PartnerCatalogCategories.Add(category);
        db.SaveChanges();
        return category;
    }

    [Fact]
    public async Task ValidateParent_ParentDoesNotExist_Fails()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);

        var result = await PartnerCatalogCategoryRules.ValidateParentAsync(db, partner.Id, Guid.NewGuid());

        Assert.False(result.Ok);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task ValidateParent_ParentIsSubcategory_Fails()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var root = AddCategory(db, tenantId, partner.Id, "Calzado");
        var child = AddCategory(db, tenantId, partner.Id, "Varón", root.Id);

        // Tercer nivel: subcategoría de una subcategoría → rechazado.
        var result = await PartnerCatalogCategoryRules.ValidateParentAsync(db, partner.Id, child.Id);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task ValidateParent_CategoryWithChildren_CannotBecomeChild()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var root = AddCategory(db, tenantId, partner.Id, "Calzado");
        AddCategory(db, tenantId, partner.Id, "Varón", root.Id);
        var otherRoot = AddCategory(db, tenantId, partner.Id, "Ropa");

        var result = await PartnerCatalogCategoryRules.ValidateParentAsync(db, partner.Id, otherRoot.Id, categoryId: root.Id);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task ValidateParent_SelfParent_Fails()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var root = AddCategory(db, tenantId, partner.Id, "Calzado");

        var result = await PartnerCatalogCategoryRules.ValidateParentAsync(db, partner.Id, root.Id, categoryId: root.Id);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task ValidateParent_RootParent_Ok()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var root = AddCategory(db, tenantId, partner.Id, "Calzado");

        var result = await PartnerCatalogCategoryRules.ValidateParentAsync(db, partner.Id, root.Id);

        Assert.True(result.Ok);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task CanDelete_WithChildren_Fails()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var root = AddCategory(db, tenantId, partner.Id, "Calzado");
        AddCategory(db, tenantId, partner.Id, "Mujer", root.Id);

        var result = await PartnerCatalogCategoryRules.CanDeleteAsync(db, root.Id);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task CanDelete_WithProducts_Fails()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, product) = TestDb.SeedCatalog(db, stock: 1);
        var root = AddCategory(db, tenantId, partner.Id, "Calzado");
        product.PartnerCatalogCategoryId = root.Id;
        db.SaveChanges();

        var result = await PartnerCatalogCategoryRules.CanDeleteAsync(db, root.Id);

        Assert.False(result.Ok);
    }

    [Fact]
    public async Task CanDelete_EmptyCategory_Ok()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var root = AddCategory(db, tenantId, partner.Id, "Calzado");

        var result = await PartnerCatalogCategoryRules.CanDeleteAsync(db, root.Id);

        Assert.True(result.Ok);
    }
}
