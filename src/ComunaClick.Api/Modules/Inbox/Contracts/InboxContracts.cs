namespace ComunaClick.Api.Modules.Inbox.Contracts;

public sealed record InboxThreadCreateRequest(
    Guid? TenantId,
    Guid? PartnerId,
    Guid? ProfessionalId,
    string Subject,
    string Body);

public sealed record InboxMessageCreateRequest(string Body);

/// <summary>Mensaje de un visitante sin cuenta: nombre y teléfono obligatorios, correo opcional.</summary>
public sealed record GuestInboxThreadCreateRequest(
    Guid? PartnerId,
    Guid? ProfessionalId,
    string FullName,
    string Phone,
    string? Email,
    string? Subject,
    string Body);

public sealed record InboxThreadStatusRequest(string Status);
