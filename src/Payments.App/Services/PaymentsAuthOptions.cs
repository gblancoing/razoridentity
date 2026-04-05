namespace Payments.App.Services;

public sealed class PaymentsAuthOptions
{
    public string BaseUrl { get; set; } = "https://acl.comunaclic.cl";
    public Guid? DefaultTenantId { get; set; }
}
