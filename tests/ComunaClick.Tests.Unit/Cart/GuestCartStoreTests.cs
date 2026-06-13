using ComunaClick.SharedUI.Services;
using Xunit;

namespace ComunaClick.Tests.Unit.Cart;

public sealed class GuestCartStoreTests
{
    [Fact]
    public void Parse_V1SinglePartnerShape_ReturnsOneGroup()
    {
        var partnerId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var raw = $$"""
        {
          "tenantId": "{{tenantId}}",
          "partnerId": "{{partnerId}}",
          "partnerName": "Almacén Central",
          "currency": "CLP",
          "items": [{ "productId": "{{productId}}", "name": "Pan", "quantity": 2, "unitPrice": 1500, "maxAvailable": 10 }]
        }
        """;

        var groups = GuestCartStore.Parse(raw);

        var group = Assert.Single(groups);
        Assert.Equal(partnerId, group.PartnerId);
        Assert.Equal(tenantId, group.TenantId);
        Assert.Equal("Almacén Central", group.PartnerName);
        var item = Assert.Single(group.Items);
        Assert.Equal(productId, item.ProductId);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(3000d, group.Subtotal);
    }

    [Fact]
    public void Parse_V2MultiGroupShape_ReturnsAllGroups()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var raw = $$"""
        {
          "version": 2,
          "groups": [
            { "tenantId": "{{Guid.NewGuid()}}", "partnerId": "{{p1}}", "partnerName": "A", "currency": "CLP",
              "items": [{ "productId": "{{Guid.NewGuid()}}", "name": "X", "quantity": 1, "unitPrice": 1000, "maxAvailable": 5 }] },
            { "tenantId": "{{Guid.NewGuid()}}", "partnerId": "{{p2}}", "partnerName": "B", "currency": "CLP",
              "items": [{ "productId": "{{Guid.NewGuid()}}", "name": "Y", "quantity": 3, "unitPrice": 500, "maxAvailable": 5 }] }
          ]
        }
        """;

        var groups = GuestCartStore.Parse(raw);

        Assert.Equal(2, groups.Count);
        Assert.Equal(p1, groups[0].PartnerId);
        Assert.Equal(p2, groups[1].PartnerId);
        Assert.Equal(1500d, groups[1].Subtotal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no es json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"partnerId\":\"no-guid\",\"items\":[]}")]
    public void Parse_GarbageOrEmpty_ReturnsEmptyList(string? raw)
    {
        Assert.Empty(GuestCartStore.Parse(raw));
    }

    [Fact]
    public void SerializeThenParse_RoundTripsStable()
    {
        var groups = new List<GuestCartGroupModel>
        {
            new()
            {
                TenantId = Guid.NewGuid(),
                PartnerId = Guid.NewGuid(),
                PartnerName = "Negocio Uno",
                Currency = "CLP",
                Items =
                {
                    new GuestCartItemModel { ProductId = Guid.NewGuid(), Name = "Café", Quantity = 2, UnitPrice = 4500, MaxAvailable = 8 },
                    new GuestCartItemModel { ProductId = Guid.NewGuid(), Name = "Té", Quantity = 1, UnitPrice = 2500, MaxAvailable = 4 }
                }
            },
            new()
            {
                TenantId = Guid.NewGuid(),
                PartnerId = Guid.NewGuid(),
                PartnerName = "Negocio Dos",
                Currency = "CLP",
                Items = { new GuestCartItemModel { ProductId = Guid.NewGuid(), Name = "Pan", Quantity = 5, UnitPrice = 300, MaxAvailable = 20 } }
            }
        };

        var json = GuestCartStore.Serialize(groups);
        var parsed = GuestCartStore.Parse(json);

        Assert.Contains("\"version\":2", json);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(groups[0].PartnerId, parsed[0].PartnerId);
        Assert.Equal(groups[0].Subtotal, parsed[0].Subtotal);
        Assert.Equal(groups[1].Items[0].Name, parsed[1].Items[0].Name);
        Assert.Equal(groups[1].Items[0].MaxAvailable, parsed[1].Items[0].MaxAvailable);
    }

    [Fact]
    public void Serialize_SkipsEmptyGroups()
    {
        var groups = new List<GuestCartGroupModel>
        {
            new() { PartnerId = Guid.NewGuid(), PartnerName = "Vacío" },
            new()
            {
                PartnerId = Guid.NewGuid(),
                PartnerName = "Con items",
                Items = { new GuestCartItemModel { ProductId = Guid.NewGuid(), UnitPrice = 100 } }
            }
        };

        var parsed = GuestCartStore.Parse(GuestCartStore.Serialize(groups));

        var group = Assert.Single(parsed);
        Assert.Equal("Con items", group.PartnerName);
    }
}
