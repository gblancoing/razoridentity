namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Rutas del centro de cuenta (sidebar) y redirecciones desde URLs legadas.
/// </summary>
public static class AccountRoutes
{
    public const string Profile = "/account/profile";
    public const string Buyer = "/account/buyer";
    public const string Messages = "/account/messages";
    public const string ProfessionalMessages = "/account/professional/messages";
    public const string Professional = "/account/professional";

    public static string CompanyNavHref(bool hasPartnerPanel)
        => hasPartnerPanel ? PartnerSettings : Company;
    public const string Company = "/account/company";
    public const string PartnerProfile = "/partner/account";
    public const string PartnerSettings = "/partner/settings";
    /// <summary>Ruta legada; redirige a ajuste de empresa.</summary>
    public const string PartnerCompany = PartnerSettings;

    public static string DefaultHome(bool hasPartnerContext)
        => hasPartnerContext ? PartnerProfile : Profile;

    public static string MyAccountLink(bool hasPartnerContext)
        => DefaultHome(hasPartnerContext);

    public static string ResolveLegacyBuyerProfilePath(Uri uri, bool hasPartnerContext)
    {
        var tab = GetQueryValue(uri, "tab");
        var wizard = uri.Query.Contains("wizard=1", StringComparison.OrdinalIgnoreCase)
            || uri.Query.Contains("wizard=true", StringComparison.OrdinalIgnoreCase);
        var wizardSuffix = wizard ? "?wizard=1" : string.Empty;

        if (hasPartnerContext)
        {
            return tab switch
            {
                "company" or "empresa" => PartnerCompany + wizardSuffix,
                "buyer" => Buyer,
                "professional" => Professional,
                _ => PartnerProfile + wizardSuffix
            };
        }

        return tab switch
        {
            "company" or "empresa" => Company,
            "buyer" => Buyer,
            "professional" => Professional,
            _ => Profile + wizardSuffix
        };
    }

    public static string BuildProfilePathWithWizard(bool profileGeoComplete, string? returnUrl)
    {
        if (profileGeoComplete
            && !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("/login", StringComparison.OrdinalIgnoreCase))
        {
            return returnUrl;
        }

        return profileGeoComplete ? Profile : $"{Profile}?wizard=1";
    }

    private static string? GetQueryValue(Uri uri, string key)
    {
        var q = uri.Query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(q))
        {
            return null;
        }

        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (!string.Equals(Uri.UnescapeDataString(parts[0]), key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
        }

        return null;
    }
}
