namespace ComunaClick.Acl.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSuperAdmin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserTenantScope> TenantScopes { get; set; } = new List<UserTenantScope>();
    public ICollection<UserTenantAccess> TenantAccess { get; set; } = new List<UserTenantAccess>();
    public ICollection<UserRegionAccess> RegionAccess { get; set; } = new List<UserRegionAccess>();
    public ICollection<UserCountryAccess> CountryAccess { get; set; } = new List<UserCountryAccess>();
    public ICollection<UserPlatformAccess> PlatformAccess { get; set; } = new List<UserPlatformAccess>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
