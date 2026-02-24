namespace ComunaClick.Acl.Security;

public interface IExternalTokenValidator
{
    string Provider { get; }
    Task<ExternalUserInfo?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}
