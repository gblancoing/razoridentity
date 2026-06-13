namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Rutas post-registro / post-OAuth unificadas para los tres perfiles de cuenta.
/// </summary>
public static class RegistrationFlow
{
    public const string RoleNatural = "natural";
    public const string RoleCommerce = "commerce";
    public const string RoleProfessional = "professional";
    public const string RoleCourier = "courier";

    public static string NormalizeRole(string? role)
    {
        if (string.Equals(role, RoleCommerce, StringComparison.OrdinalIgnoreCase)) return RoleCommerce;
        if (string.Equals(role, RoleProfessional, StringComparison.OrdinalIgnoreCase)) return RoleProfessional;
        if (string.Equals(role, RoleCourier, StringComparison.OrdinalIgnoreCase)) return RoleCourier;
        return RoleNatural;
    }

    public static bool IsPartnerRole(string? role)
        => string.Equals(role, RoleCommerce, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, RoleProfessional, StringComparison.OrdinalIgnoreCase);

    public static string PartnerOnboardingPath(string? role)
        => string.Equals(NormalizeRole(role), RoleProfessional, StringComparison.OrdinalIgnoreCase)
            ? "/register/business?defaultType=C&wizard=1"
            : "/register/business?defaultType=A&wizard=1";

    public static string ResolvePostAuthDestination(string? role, string? returnUrl)
    {
        var normalized = NormalizeRole(role);
        if (string.Equals(normalized, RoleCommerce, StringComparison.OrdinalIgnoreCase))
        {
            return PartnerOnboardingPath(RoleCommerce);
        }

        if (string.Equals(normalized, RoleProfessional, StringComparison.OrdinalIgnoreCase))
        {
            return PartnerOnboardingPath(RoleProfessional);
        }

        if (string.Equals(normalized, RoleCourier, StringComparison.OrdinalIgnoreCase))
        {
            // Sin onboarding extra: si el negocio registró su correo, el panel
            // se llena solo; si no, muestra el empty-state con su correo.
            return AccountRoutes.Courier + "?welcome=1";
        }

        return BuildBuyerCompletePath(returnUrl);
    }

    public static string BuildBuyerCompletePath(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/'))
        {
            return $"/register/complete/buyer?returnUrl={Uri.EscapeDataString(returnUrl)}";
        }

        return "/register/complete/buyer";
    }

    public static string ResolveBuyerProfilePath(string? returnUrl, bool profileGeoComplete)
    {
        if (profileGeoComplete
            && !string.IsNullOrWhiteSpace(returnUrl)
            && returnUrl.StartsWith('/')
            && !returnUrl.StartsWith("/login", StringComparison.OrdinalIgnoreCase))
        {
            return returnUrl;
        }

        return AccountRoutes.BuildProfilePathWithWizard(profileGeoComplete, returnUrl);
    }

    public static int WizardTotalSteps(string flow) => flow switch
    {
        RoleCommerce => 3,
        RoleProfessional => 3,
        _ => 3
    };
}
