namespace ComunaClick.Api.Modules.Onboarding.Contracts.Tenants;

public sealed record TenantUpdateRequest(string? Name, string? Timezone, string? ConfigJson);
