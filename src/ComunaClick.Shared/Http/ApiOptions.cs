namespace ComunaClick.Shared.Http;

public sealed class ApiOptions
{
    public string ApiBaseUrl { get; init; } = string.Empty;
    public string AclBaseUrl { get; init; } = string.Empty;
    public Guid? DefaultPartnerId { get; init; }
    public Guid? DefaultTenantId { get; init; }
    public Guid? DefaultProfessionalId { get; init; }
}
