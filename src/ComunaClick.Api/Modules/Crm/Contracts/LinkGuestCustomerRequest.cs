namespace ComunaClick.Api.Modules.Crm.Contracts;

public sealed record LinkGuestCustomerRequest(Guid CustomerId, Guid? TenantId);
