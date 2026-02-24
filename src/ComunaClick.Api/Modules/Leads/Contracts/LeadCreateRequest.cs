namespace ComunaClick.Api.Modules.Leads.Contracts;

public sealed record LeadCreateRequest(
    Guid ProfessionalId,
    Guid CustomerId,
    string? Message);
