namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record CustomerCreateRequest(
    string? Email,
    string? Phone,
    string? FullName);
