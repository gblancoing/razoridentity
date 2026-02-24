namespace ComunaClick.Api.Modules.Support.Contracts;

public sealed record SupportTicketRequest(
    Guid? CustomerId,
    Guid? PartnerId,
    string? Email,
    string? Name,
    string? Topic,
    string? Message);
