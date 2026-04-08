using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Persistence;

public sealed class CoreDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public CoreDbContext(DbContextOptions<CoreDbContext> options, ITenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<Comuna> Comunas => Set<Comuna>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<PartnerStaff> PartnerStaff => Set<PartnerStaff>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductInventory> ProductInventories => Set<ProductInventory>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductSubcategory> ProductSubcategories => Set<ProductSubcategory>();
    public DbSet<SiteContentSetting> SiteContentSettings => Set<SiteContentSetting>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerPartnerLink> CustomerPartnerLinks => Set<CustomerPartnerLink>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceSlot> ServiceSlots => Set<ServiceSlot>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<PayoutBatch> PayoutBatches => Set<PayoutBatch>();
    public DbSet<PayoutItem> PayoutItems => Set<PayoutItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        var tenantId = _tenantContext.TenantId;
        modelBuilder.Entity<Tenant>().HasQueryFilter(x => !tenantId.HasValue || x.Id == tenantId.Value);
        modelBuilder.Entity<Partner>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<PartnerStaff>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<SubscriptionPlan>().HasQueryFilter(x =>
            !tenantId.HasValue || x.TenantId == tenantId.Value || x.TenantId == null);
        modelBuilder.Entity<Subscription>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Order>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(x => !tenantId.HasValue || x.Order.TenantId == tenantId.Value);
        modelBuilder.Entity<Product>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<ProductInventory>().HasQueryFilter(x => !tenantId.HasValue || x.Product.TenantId == tenantId.Value);
        modelBuilder.Entity<Customer>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<CustomerPartnerLink>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Interaction>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Professional>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Service>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<ServiceSlot>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Lead>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Booking>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<PaymentEvent>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<PayoutBatch>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<PayoutItem>().HasQueryFilter(x => !tenantId.HasValue || x.Batch.TenantId == tenantId.Value);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Timezone).HasColumnName("timezone").HasDefaultValue("America/Santiago");
            entity.Property(x => x.ConfigJson).HasColumnName("config_json").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Name).IsUnique();
            entity.HasIndex(x => x.ComunaId).IsUnique();
            entity.HasOne(x => x.Comuna).WithMany(x => x.Tenants).HasForeignKey(x => x.ComunaId);
        });

        modelBuilder.Entity<Country>(entity =>
        {
            entity.ToTable("countries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Code).HasColumnName("code").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Region>(entity =>
        {
            entity.ToTable("regions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.Property(x => x.Code).HasColumnName("code").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.LegacyRegionId).HasColumnName("legacy_region_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.CountryId);
            entity.HasIndex(x => x.LegacyRegionId)
                .HasFilter("legacy_region_id IS NOT NULL");
            entity.HasOne(x => x.Country).WithMany(x => x.Regions).HasForeignKey(x => x.CountryId);
        });

        modelBuilder.Entity<Comuna>(entity =>
        {
            entity.ToTable("comunas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.Code).HasColumnName("code").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.LegacyComunaId).HasColumnName("legacy_comuna_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasColumnType("double precision");
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasColumnType("double precision");
            entity.HasIndex(x => x.RegionId);
            entity.HasIndex(x => x.LegacyComunaId)
                .HasFilter("legacy_comuna_id IS NOT NULL");
            entity.HasOne(x => x.Region).WithMany(x => x.Comunas).HasForeignKey(x => x.RegionId);
        });

        modelBuilder.Entity<Partner>(entity =>
        {
            entity.ToTable("partners");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.SubcategoryId).HasColumnName("subcategory_id");
            entity.Property(x => x.Type).HasColumnName("type").HasColumnType("char(1)").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Rut).HasColumnName("rut");
            entity.Property(x => x.Address).HasColumnName("address");
            entity.Property(x => x.Phone).HasColumnName("phone");
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasColumnType("double precision");
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasColumnType("double precision");
            entity.Property(x => x.IsVisible).HasColumnName("is_visible").HasDefaultValue(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasIndex(x => x.SubcategoryId);
            entity.HasOne(x => x.Tenant).WithMany(x => x.Partners).HasForeignKey(x => x.TenantId);
            entity.HasOne(x => x.Subcategory).WithMany(x => x.Partners).HasForeignKey(x => x.SubcategoryId);
        });

        modelBuilder.Entity<ProductCategory>(entity =>
        {
            entity.ToTable("product_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Code).HasColumnName("code").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<ProductSubcategory>(entity =>
        {
            entity.ToTable("product_subcategories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.CategoryId).HasColumnName("category_id");
            entity.Property(x => x.Code).HasColumnName("code").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.CategoryId);
            entity.HasOne(x => x.Category).WithMany(x => x.Subcategories).HasForeignKey(x => x.CategoryId);
        });

        modelBuilder.Entity<SiteContentSetting>(entity =>
        {
            entity.ToTable("site_content_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Section).HasColumnName("section").IsRequired();
            entity.Property(x => x.ContentJson).HasColumnName("content_json").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Section).IsUnique();
        });

        modelBuilder.Entity<PartnerStaff>(entity =>
        {
            entity.ToTable("partner_staff");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Role).HasColumnName("role").HasDefaultValue("staff");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.PartnerId, x.UserId }).IsUnique();
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("subscription_plans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Code).HasColumnName("code").IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.MonthlyPrice).HasColumnName("monthly_price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.CommissionPct).HasColumnName("commission_pct").HasColumnType("numeric(6,3)");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.PlanId).HasColumnName("plan_id");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.CurrentPeriodStart).HasColumnName("current_period_start").HasDefaultValueSql("now()");
            entity.Property(x => x.CurrentPeriodEnd).HasColumnName("current_period_end");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(14,2)");
            entity.Property(x => x.DeliveryFee).HasColumnName("delivery_fee").HasColumnType("numeric(14,2)");
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.ToTable("order_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.OrderId).HasColumnName("order_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.TotalPrice).HasColumnName("total_price").HasColumnType("numeric(14,2)");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.Category).HasColumnName("category");
            entity.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ProductInventory>(entity =>
        {
            entity.ToTable("product_inventory");
            entity.HasKey(x => x.ProductId);
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasDefaultValue(0);
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne(x => x.Product).WithOne(x => x.Inventory).HasForeignKey<ProductInventory>(x => x.ProductId);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.Phone).HasColumnName("phone");
            entity.Property(x => x.FullName).HasColumnName("full_name");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        });

        modelBuilder.Entity<CustomerPartnerLink>(entity =>
        {
            entity.ToTable("customer_partner_links");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.FirstSeenAt).HasColumnName("first_seen_at").HasDefaultValueSql("now()");
            entity.Property(x => x.LastSeenAt).HasColumnName("last_seen_at").HasDefaultValueSql("now()");
            entity.Property(x => x.Source).HasColumnName("source");
            entity.HasIndex(x => new { x.CustomerId, x.PartnerId }).IsUnique();
        });

        modelBuilder.Entity<Interaction>(entity =>
        {
            entity.ToTable("interactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.Type).HasColumnName("type").IsRequired();
            entity.Property(x => x.ReferenceId).HasColumnName("reference_id");
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Professional>(entity =>
        {
            entity.ToTable("professionals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.Phone).HasColumnName("phone");
            entity.Property(x => x.Specialty).HasColumnName("specialty");
            entity.Property(x => x.Bio).HasColumnName("bio");
            entity.Property(x => x.IsVerified).HasColumnName("is_verified").HasDefaultValue(false);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("services");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.Category).HasColumnName("category");
            entity.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.DurationMinutes).HasColumnName("duration_minutes").HasDefaultValue(30);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ServiceSlot>(entity =>
        {
            entity.ToTable("service_slots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.StartAt).HasColumnName("start_at");
            entity.Property(x => x.EndAt).HasColumnName("end_at");
            entity.Property(x => x.Capacity).HasColumnName("capacity").HasDefaultValue(1);
            entity.Property(x => x.IsAvailable).HasColumnName("is_available").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.ServiceId, x.StartAt }).IsUnique();
        });

        modelBuilder.Entity<Lead>(entity =>
        {
            entity.ToTable("leads");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.ProfessionalId).HasColumnName("professional_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.SlotId).HasColumnName("slot_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.StartAt).HasColumnName("start_at");
            entity.Property(x => x.EndAt).HasColumnName("end_at");
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.CancellationPolicy).HasColumnName("cancellation_policy").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Provider).HasColumnName("provider").HasDefaultValue("transbank");
            entity.Property(x => x.ExternalReference).HasColumnName("external_reference");
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.GatewayIntentId).HasColumnName("gateway_intent_id");
            entity.Property(x => x.ProviderToken).HasColumnName("provider_token");
            entity.Property(x => x.LastEventId).HasColumnName("last_event_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Provider, x.ExternalReference }).IsUnique();
        });

        modelBuilder.Entity<PaymentEvent>(entity =>
        {
            entity.ToTable("payment_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.ProviderEventId).HasColumnName("provider_event_id");
            entity.Property(x => x.PaymentId).HasColumnName("payment_id");
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.ProviderEventId }).IsUnique();
        });

        modelBuilder.Entity<PayoutBatch>(entity =>
        {
            entity.ToTable("payout_batches");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PeriodStart).HasColumnName("period_start").HasColumnType("date");
            entity.Property(x => x.PeriodEnd).HasColumnName("period_end").HasColumnType("date");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<PayoutItem>(entity =>
        {
            entity.ToTable("payout_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.BatchId).HasColumnName("batch_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.GrossAmount).HasColumnName("gross_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.CommissionAmount).HasColumnName("commission_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.SubscriptionDeduction).HasColumnName("subscription_deduction").HasColumnType("numeric(14,2)");
            entity.Property(x => x.NetAmount).HasColumnName("net_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.BatchId, x.PartnerId }).IsUnique();
            entity.HasOne(x => x.Batch).WithMany(x => x.Items).HasForeignKey(x => x.BatchId);
        });
    }
}
