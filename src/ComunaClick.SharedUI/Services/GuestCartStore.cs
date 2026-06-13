using System.Text.Json;

namespace ComunaClick.SharedUI.Services;

public sealed class GuestCartItemModel
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = "Producto";
    public int Quantity { get; set; } = 1;
    public double UnitPrice { get; set; }
    public int MaxAvailable { get; set; } = 99;
}

public sealed class GuestCartGroupModel
{
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public string Currency { get; set; } = "CLP";
    public List<GuestCartItemModel> Items { get; set; } = new();

    public double Subtotal => Items.Sum(x => x.UnitPrice * x.Quantity);
    public int ItemCount => Items.Sum(x => x.Quantity);
}

/// <summary>
/// Lectura/escritura del carrito de invitado en localStorage
/// (key <c>comunaclic.guestCart</c>). Soporta el shape v1 (un negocio en la
/// raíz) y el v2 multi-negocio (<c>{ version: 2, groups: [...] }</c>); siempre
/// serializa v2. Es el espejo C# de los helpers de app.js.
/// </summary>
public static class GuestCartStore
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static List<GuestCartGroupModel> Parse(string? rawJson)
    {
        var groups = new List<GuestCartGroupModel>();
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return groups;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return groups;
            }

            if (root.TryGetProperty("groups", out var groupsElement) && groupsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var groupElement in groupsElement.EnumerateArray())
                {
                    if (TryParseGroup(groupElement, out var group))
                    {
                        groups.Add(group);
                    }
                }
            }
            else if (TryParseGroup(root, out var legacyGroup))
            {
                // Shape v1: el carrito completo era un único negocio en la raíz.
                groups.Add(legacyGroup);
            }
        }
        catch (JsonException)
        {
            // Carrito corrupto: se trata como vacío.
        }

        return groups;
    }

    public static string Serialize(IReadOnlyList<GuestCartGroupModel> groups)
        => JsonSerializer.Serialize(new
        {
            version = 2,
            groups = groups
                .Where(g => g.PartnerId != Guid.Empty && g.Items.Count > 0)
                .Select(g => new
                {
                    tenantId = g.TenantId,
                    partnerId = g.PartnerId,
                    partnerName = g.PartnerName,
                    currency = g.Currency,
                    items = g.Items.Select(x => new
                    {
                        productId = x.ProductId,
                        name = x.Name,
                        quantity = x.Quantity,
                        unitPrice = x.UnitPrice,
                        maxAvailable = x.MaxAvailable
                    })
                })
        }, WriteOptions);

    private static bool TryParseGroup(JsonElement element, out GuestCartGroupModel group)
    {
        group = new GuestCartGroupModel();
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!element.TryGetProperty("partnerId", out var partnerIdElement)
            || !Guid.TryParse(partnerIdElement.GetString(), out var partnerId)
            || partnerId == Guid.Empty)
        {
            return false;
        }

        group.PartnerId = partnerId;
        if (element.TryGetProperty("tenantId", out var tenantElement) && Guid.TryParse(tenantElement.GetString(), out var tenantId))
        {
            group.TenantId = tenantId;
        }

        if (element.TryGetProperty("partnerName", out var nameElement))
        {
            group.PartnerName = nameElement.GetString() ?? string.Empty;
        }

        if (element.TryGetProperty("currency", out var currencyElement))
        {
            group.Currency = string.IsNullOrWhiteSpace(currencyElement.GetString()) ? "CLP" : currencyElement.GetString()!;
        }

        if (!element.TryGetProperty("items", out var itemsElement) || itemsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var itemElement in itemsElement.EnumerateArray())
        {
            if (!itemElement.TryGetProperty("productId", out var productIdElement)
                || !Guid.TryParse(productIdElement.GetString(), out var productId))
            {
                continue;
            }

            group.Items.Add(new GuestCartItemModel
            {
                ProductId = productId,
                Name = itemElement.TryGetProperty("name", out var n) ? n.GetString() ?? "Producto" : "Producto",
                Quantity = itemElement.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var qi) ? Math.Max(1, qi) : 1,
                UnitPrice = itemElement.TryGetProperty("unitPrice", out var up) && up.TryGetDouble(out var price) ? price : 0,
                MaxAvailable = itemElement.TryGetProperty("maxAvailable", out var m) && m.TryGetInt32(out var max) ? max : 99
            });
        }

        return group.Items.Count > 0;
    }
}
