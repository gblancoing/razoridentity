using ComunaClick.Acl.Domain;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Acl.Persistence;

public sealed class AclDbContext : DbContext
{
    public AclDbContext(DbContextOptions<AclDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserTenantScope> UserTenantScopes => Set<UserTenantScope>();
    public DbSet<UserTenantAccess> UserTenantAccess => Set<UserTenantAccess>();
    public DbSet<UserRegionAccess> UserRegionAccess => Set<UserRegionAccess>();
    public DbSet<UserCountryAccess> UserCountryAccess => Set<UserCountryAccess>();
    public DbSet<UserPlatformAccess> UserPlatformAccess => Set<UserPlatformAccess>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("acl");

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Email).HasColumnName("email").IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            entity.Property(x => x.DisplayName).HasColumnName("display_name");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.IsSuperAdmin).HasColumnName("is_super_admin").HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.IsSuperAdmin);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Code).HasColumnName("code").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions");
            entity.HasKey(x => new { x.RoleId, x.PermissionId });
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.PermissionId).HasColumnName("permission_id");
            entity.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId);
            entity.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<UserTenantScope>(entity =>
        {
            entity.ToTable("user_tenant_scope");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.ScopeType).HasColumnName("scope_type").HasDefaultValue("tenant");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => new { x.UserId, x.TenantId, x.PartnerId, x.ScopeType }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.TenantScopes).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserTenantAccess>(entity =>
        {
            entity.ToTable("user_tenant_access");
            entity.HasKey(x => new { x.UserId, x.TenantId });
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.TenantId);
            entity.HasOne(x => x.User).WithMany(x => x.TenantAccess).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserRegionAccess>(entity =>
        {
            entity.ToTable("user_region_access");
            entity.HasKey(x => new { x.UserId, x.RegionId });
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.RegionId);
            entity.HasOne(x => x.User).WithMany(x => x.RegionAccess).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserCountryAccess>(entity =>
        {
            entity.ToTable("user_country_access");
            entity.HasKey(x => new { x.UserId, x.CountryId });
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.CountryId);
            entity.HasOne(x => x.User).WithMany(x => x.CountryAccess).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<UserPlatformAccess>(entity =>
        {
            entity.ToTable("user_platform_access");
            entity.HasKey(x => x.UserId);
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.HasOne(x => x.User).WithMany(x => x.PlatformAccess).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(x => x.IssuedAt).HasColumnName("issued_at").HasDefaultValueSql("now()");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            entity.Property(x => x.UserAgent).HasColumnName("user_agent");
            entity.Property(x => x.IpAddress).HasColumnName("ip_address");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ExpiresAt);
            entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            entity.Property(x => x.IssuedAt).HasColumnName("issued_at").HasDefaultValueSql("now()");
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            entity.Property(x => x.UsedAt).HasColumnName("used_at");
            entity.Property(x => x.UserAgent).HasColumnName("user_agent");
            entity.Property(x => x.IpAddress).HasColumnName("ip_address");
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ExpiresAt);
            entity.HasOne(x => x.User).WithMany(x => x.PasswordResetTokens).HasForeignKey(x => x.UserId);
        });
    }
}
