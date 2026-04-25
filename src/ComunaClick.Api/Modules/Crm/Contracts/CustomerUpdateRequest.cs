namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record CustomerUpdateRequest(string? Email, string? Phone, string? FullName, string? AvatarUrl = null);
