namespace ComunaClick.Api.Modules.Inbox.Contracts;

public sealed record InboxThreadCreateRequest(
    Guid? TenantId,
    Guid? PartnerId,
    Guid? ProfessionalId,
    string Subject,
    string Body);

public sealed record InboxMessageCreateRequest(string Body);

public sealed record InboxThreadStatusRequest(string Status);
