namespace ComunaClick.Api.Modules.Onboarding.Contracts.Tenants;

public sealed record TenantCreateRequest(string Name, string? Timezone, string? ConfigJson);
