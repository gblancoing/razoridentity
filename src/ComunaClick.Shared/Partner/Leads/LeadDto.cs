namespace ComunaClick.Shared.Partner.Leads;

public sealed record LeadDto(
    Guid Id,
    string Name,
    string Details,
    string Status,
    DateTimeOffset CreatedAt
);
