namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record AddCertificationRequest(
    string Name,
    string? Institution,
    int? Year,
    string? Url);
