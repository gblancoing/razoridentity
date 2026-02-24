namespace ComunaClick.Acl.Domain;

public sealed class UserCountryAccess
{
    public Guid UserId { get; set; }
    public Guid CountryId { get; set; }

    public User? User { get; set; }
}
