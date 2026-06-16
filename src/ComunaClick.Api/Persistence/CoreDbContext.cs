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
    public DbSet<ShoppingCart> ShoppingCarts => Set<ShoppingCart>();
    public DbSet<ShoppingCartItem> ShoppingCartItems => Set<ShoppingCartItem>();
    public DbSet<DeliveryProvider> DeliveryProviders => Set<DeliveryProvider>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductInventory> ProductInventories => Set<ProductInventory>();
    public DbSet<ProductDiscoverySubcategory> ProductDiscoverySubcategories => Set<ProductDiscoverySubcategory>();
    public DbSet<PartnerCatalogCategory> PartnerCatalogCategories => Set<PartnerCatalogCategory>();
    public DbSet<Courier> Couriers => Set<Courier>();
    public DbSet<DeliveryTracking> DeliveryTrackings => Set<DeliveryTracking>();
    public DbSet<DeliverySettlement> DeliverySettlements => Set<DeliverySettlement>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductSubcategory> ProductSubcategories => Set<ProductSubcategory>();
    public DbSet<SiteContentSetting> SiteContentSettings => Set<SiteContentSetting>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<BuyerFavorite> BuyerFavorites => Set<BuyerFavorite>();
    public DbSet<CustomerPartnerLink> CustomerPartnerLinks => Set<CustomerPartnerLink>();
    public DbSet<Interaction> Interactions => Set<Interaction>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<ServiceProfessional> ServiceProfessionals => Set<ServiceProfessional>();
    public DbSet<ServiceImage> ServiceImages => Set<ServiceImage>();
    public DbSet<ServiceSlot> ServiceSlots => Set<ServiceSlot>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<InboxThread> InboxThreads => Set<InboxThread>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<SellerMercadoPagoAccount> SellerMercadoPagoAccounts => Set<SellerMercadoPagoAccount>();
    public DbSet<SellerFeeConfiguration> SellerFeeConfigurations => Set<SellerFeeConfiguration>();
    public DbSet<PaymentFee> PaymentFees => Set<PaymentFee>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<PaymentStatusHistory> PaymentStatusHistory => Set<PaymentStatusHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PayoutBatch> PayoutBatches => Set<PayoutBatch>();
    public DbSet<PayoutItem> PayoutItems => Set<PayoutItem>();
    public DbSet<ProfessionalFollow> ProfessionalFollows => Set<ProfessionalFollow>();
    public DbSet<NotificationOutbox> NotificationOutbox => Set<NotificationOutbox>();

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
        modelBuilder.Entity<ShoppingCart>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<ShoppingCartItem>().HasQueryFilter(x => !tenantId.HasValue || x.Cart.TenantId == tenantId.Value);
        modelBuilder.Entity<DeliveryProvider>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value || x.TenantId == null);
        modelBuilder.Entity<Product>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<ProductImage>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<ProductInventory>().HasQueryFilter(x => !tenantId.HasValue || x.Product.TenantId == tenantId.Value);
        modelBuilder.Entity<PartnerCatalogCategory>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Courier>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Customer>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<BuyerFavorite>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<CustomerPartnerLink>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Interaction>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Professional>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Service>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<ServiceSlot>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Lead>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<InboxThread>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<InboxMessage>().HasQueryFilter(x => !tenantId.HasValue || x.Thread.TenantId == tenantId.Value);
        modelBuilder.Entity<Booking>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<PaymentEvent>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<Seller>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<PayoutBatch>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);
        modelBuilder.Entity<PayoutItem>().HasQueryFilter(x => !tenantId.HasValue || x.Batch.TenantId == tenantId.Value);
        modelBuilder.Entity<NotificationOutbox>().HasQueryFilter(x => !tenantId.HasValue || x.TenantId == tenantId.Value);

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
            entity.Property(x => x.OffersServices).HasColumnName("offers_services").HasDefaultValue(false);
            entity.Property(x => x.BannerUrl).HasColumnName("banner_url");
            entity.Property(x => x.LogoUrl).HasColumnName("logo_url");
            entity.Property(x => x.StorefrontTagline).HasColumnName("storefront_tagline");
            entity.Property(x => x.StorefrontAbout).HasColumnName("storefront_about");
            entity.Property(x => x.StorefrontHighlight1).HasColumnName("storefront_highlight_1");
            entity.Property(x => x.StorefrontHighlight2).HasColumnName("storefront_highlight_2");
            entity.Property(x => x.StorefrontHighlight3).HasColumnName("storefront_highlight_3");
            entity.Property(x => x.BankName).HasColumnName("bank_name");
            entity.Property(x => x.BankAccountType).HasColumnName("bank_account_type");
            entity.Property(x => x.BankAccountNumber).HasColumnName("bank_account_number");
            entity.Property(x => x.BankAccountHolder).HasColumnName("bank_account_holder");
            entity.Property(x => x.BankAccountHolderRut).HasColumnName("bank_account_holder_rut");
            entity.Property(x => x.PreferredDeliveryProviderId).HasColumnName("preferred_delivery_provider_id");
            entity.Property(x => x.ShippingCourierPaidEnabled).HasColumnName("shipping_courier_paid_enabled").HasDefaultValue(false);
            entity.Property(x => x.ShippingFreeOverAmountEnabled).HasColumnName("shipping_free_over_amount_enabled").HasDefaultValue(false);
            entity.Property(x => x.ShippingFreeOverAmount).HasColumnName("shipping_free_over_amount").HasColumnType("numeric(12,2)");
            entity.Property(x => x.ShippingDeliveryZoneEnabled).HasColumnName("shipping_delivery_zone_enabled").HasDefaultValue(false);
            entity.Property(x => x.ShippingFreeEnabled).HasColumnName("shipping_free_enabled").HasDefaultValue(false);
            entity.Property(x => x.WebsiteUrl).HasColumnName("website_url");
            entity.Property(x => x.InstagramUrl).HasColumnName("instagram_url");
            entity.Property(x => x.FacebookUrl).HasColumnName("facebook_url");
            entity.Property(x => x.LinkedInUrl).HasColumnName("linkedin_url");
            entity.Property(x => x.XUrl).HasColumnName("x_url");
            entity.Property(x => x.TikTokUrl).HasColumnName("tiktok_url");
            entity.Property(x => x.YouTubeUrl).HasColumnName("youtube_url");
            entity.Property(x => x.OtherLinkLabel).HasColumnName("other_link_label");
            entity.Property(x => x.OtherLinkUrl).HasColumnName("other_link_url");
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
            entity.Property(x => x.ImageUrl).HasColumnName("image_url");
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CatalogScope).HasColumnName("catalog_scope").HasMaxLength(32).HasDefaultValue("commerce");
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.CatalogScope);
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
            entity.Property(x => x.ExternalReference).HasColumnName("external_reference");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(14,2)");
            entity.Property(x => x.DeliveryFee).HasColumnName("delivery_fee").HasColumnType("numeric(14,2)");
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.GrossAmount).HasColumnName("gross_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.PlatformFeeAmount).HasColumnName("platform_fee_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.NetAmount).HasColumnName("net_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.BuyerEmail).HasColumnName("buyer_email");
            entity.Property(x => x.BuyerName).HasColumnName("buyer_name");
            entity.Property(x => x.DeliveryProviderId).HasColumnName("delivery_provider_id");
            entity.Property(x => x.DeliveryProviderName).HasColumnName("delivery_provider_name");
            entity.Property(x => x.DeliveryAddress).HasColumnName("delivery_address");
            entity.Property(x => x.DeliveryType).HasColumnName("delivery_type");
            entity.Property(x => x.DeliveryStatus).HasColumnName("delivery_status");
            entity.Property(x => x.CourierId).HasColumnName("courier_id");
            entity.Property(x => x.CourierTokenKey).HasColumnName("courier_token_key");
            entity.Property(x => x.OriginLat).HasColumnName("origin_lat");
            entity.Property(x => x.OriginLng).HasColumnName("origin_lng");
            entity.Property(x => x.OriginAddress).HasColumnName("origin_address");
            entity.Property(x => x.DestinationLat).HasColumnName("destination_lat");
            entity.Property(x => x.DestinationLng).HasColumnName("destination_lng");
            entity.Property(x => x.InventoryFulfilledAt).HasColumnName("inventory_fulfilled_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.ExternalReference })
                .IsUnique()
                .HasFilter("external_reference IS NOT NULL");
            entity.HasMany(x => x.Items).WithOne(x => x.Order).HasForeignKey(x => x.OrderId);
        });

        modelBuilder.Entity<Courier>(entity =>
        {
            entity.ToTable("couriers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Phone).HasColumnName("phone").IsRequired();
            entity.Property(x => x.Company).HasColumnName("company");
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.Kind).HasColumnName("kind").HasDefaultValue("courier");
            entity.Property(x => x.IsAvailable).HasColumnName("is_available").HasDefaultValue(true);
            entity.Property(x => x.CurrentLat).HasColumnName("current_lat");
            entity.Property(x => x.CurrentLng).HasColumnName("current_lng");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.PartnerId);
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId);
        });

        modelBuilder.Entity<DeliveryTracking>(entity =>
        {
            entity.ToTable("delivery_tracking");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.OrderId).HasColumnName("order_id");
            entity.Property(x => x.CourierId).HasColumnName("courier_id");
            entity.Property(x => x.Lat).HasColumnName("lat");
            entity.Property(x => x.Lng).HasColumnName("lng");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.OrderId, x.CreatedAt });
        });

        modelBuilder.Entity<DeliverySettlement>(entity =>
        {
            entity.ToTable("delivery_settlements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.OrderId).HasColumnName("order_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.CourierId).HasColumnName("courier_id");
            entity.Property(x => x.PaymentId).HasColumnName("payment_id");
            entity.Property(x => x.GrossAmount).HasColumnName("gross_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.PlatformFeeAmount).HasColumnName("platform_fee_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.MercadoPagoFeeAmount).HasColumnName("mercadopago_fee_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.NetToCourierAmount).HasColumnName("net_to_courier_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.SettledAt).HasColumnName("settled_at");
            entity.Property(x => x.Notes).HasColumnName("notes");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.OrderId).IsUnique();
            entity.HasIndex(x => x.PartnerId);
            entity.HasIndex(x => x.CourierId);
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

        modelBuilder.Entity<ShoppingCart>(entity =>
        {
            entity.ToTable("shopping_carts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.Status).HasColumnName("status").HasDefaultValue("active");
            entity.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(14,2)");
            entity.Property(x => x.DeliveryFee).HasColumnName("delivery_fee").HasColumnType("numeric(14,2)");
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.CustomerId, x.PartnerId, x.Status });
            entity.HasMany(x => x.Items).WithOne(x => x.Cart).HasForeignKey(x => x.CartId);
        });

        modelBuilder.Entity<ShoppingCartItem>(entity =>
        {
            entity.ToTable("shopping_cart_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.CartId).HasColumnName("cart_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.TotalPrice).HasColumnName("total_price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
        });

        modelBuilder.Entity<DeliveryProvider>(entity =>
        {
            entity.ToTable("delivery_providers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.ContactName).HasColumnName("contact_name");
            entity.Property(x => x.ContactPhone).HasColumnName("contact_phone");
            entity.Property(x => x.ContactEmail).HasColumnName("contact_email");
            entity.Property(x => x.BaseFee).HasColumnName("base_fee").HasColumnType("numeric(14,2)");
            entity.Property(x => x.EstimatedMinutes).HasColumnName("estimated_minutes");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.RegionId, x.ComunaId, x.IsActive });
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
            entity.Property(x => x.Latitude).HasColumnName("latitude");
            entity.Property(x => x.Longitude).HasColumnName("longitude");
            entity.Property(x => x.ProductAddress).HasColumnName("product_address");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.Category).HasColumnName("category");
            entity.Property(x => x.PartnerCatalogCategoryId).HasColumnName("partner_catalog_category_id");
            entity.Property(x => x.ImageUrl).HasColumnName("image_url");
            entity.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.CostPrice).HasColumnName("cost_price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasOne(x => x.PartnerCatalogCategory).WithMany().HasForeignKey(x => x.PartnerCatalogCategoryId);
            entity.Ignore(x => x.ImageUrls);
            entity.Ignore(x => x.Images);
            entity.Ignore(x => x.CatalogCategoryName);
            entity.Ignore(x => x.DiscoverySubcategoryIds);
        });

        modelBuilder.Entity<ProductDiscoverySubcategory>(entity =>
        {
            entity.ToTable("product_discovery_subcategories");
            entity.HasKey(x => new { x.ProductId, x.SubcategoryId });
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.SubcategoryId).HasColumnName("subcategory_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
            entity.HasOne(x => x.Subcategory).WithMany().HasForeignKey(x => x.SubcategoryId);
        });

        modelBuilder.Entity<PartnerCatalogCategory>(entity =>
        {
            entity.ToTable("partner_catalog_categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.ParentId).HasColumnName("parent_id");
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.PartnerId, x.SortOrder });
            entity.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId);
            entity.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId);
        });

        modelBuilder.Entity<ProductImage>(entity =>
        {
            entity.ToTable("product_images");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.ProductId).HasColumnName("product_id");
            entity.Property(x => x.Url).HasColumnName("url").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.ProductId, x.SortOrder });
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
            entity.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.Address).HasColumnName("address");
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasColumnType("double precision");
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasColumnType("double precision");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.HasIndex(x => x.ComunaId);
        });

        modelBuilder.Entity<BuyerFavorite>(entity =>
        {
            entity.ToTable("buyer_favorites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.Type).HasColumnName("type").IsRequired();
            entity.Property(x => x.TargetId).HasColumnName("target_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.CustomerId, x.Type, x.TargetId }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.CustomerId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.PartnerId });
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
            entity.Property(x => x.ReadAt).HasColumnName("read_at");
            entity.Property(x => x.ArchivedAt).HasColumnName("archived_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.PartnerId, x.CreatedAt });
            entity.HasIndex(x => new { x.TenantId, x.PartnerId, x.ArchivedAt, x.ReadAt });
        });

        modelBuilder.Entity<Professional>(entity =>
        {
            entity.ToTable("professionals");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.CountryId).HasColumnName("country_id");
            entity.Property(x => x.RegionId).HasColumnName("region_id");
            entity.Property(x => x.ComunaId).HasColumnName("comuna_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Email).HasColumnName("email");
            entity.Property(x => x.Phone).HasColumnName("phone");
            entity.Property(x => x.Specialty).HasColumnName("specialty");
            entity.Property(x => x.Bio).HasColumnName("bio");
            entity.Property(x => x.BannerUrl).HasColumnName("banner_url");
            entity.Property(x => x.ProfilePhotoUrl).HasColumnName("profile_photo_url");
            entity.Property(x => x.ProfileViewCount).HasColumnName("profile_view_count").HasDefaultValue(0L);
            entity.Property(x => x.CertificationsJson).HasColumnName("certifications_json");
            entity.Property(x => x.ProfileHeadline).HasColumnName("profile_headline");
            entity.Property(x => x.WebsiteUrl).HasColumnName("website_url");
            entity.Property(x => x.InstagramUrl).HasColumnName("instagram_url");
            entity.Property(x => x.FacebookUrl).HasColumnName("facebook_url");
            entity.Property(x => x.LinkedInUrl).HasColumnName("linkedin_url");
            entity.Property(x => x.XUrl).HasColumnName("x_url");
            entity.Property(x => x.TikTokUrl).HasColumnName("tiktok_url");
            entity.Property(x => x.YouTubeUrl).HasColumnName("youtube_url");
            entity.Property(x => x.OtherLinkLabel).HasColumnName("other_link_label");
            entity.Property(x => x.OtherLinkUrl).HasColumnName("other_link_url");
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
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasColumnType("double precision");
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasColumnType("double precision");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.Category).HasColumnName("category");
            entity.Property(x => x.Price).HasColumnName("price").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.DurationMinutes).HasColumnName("duration_minutes").HasDefaultValue(30);
            entity.Property(x => x.IsBookable).HasColumnName("is_bookable").HasDefaultValue(false);
            entity.Property(x => x.RequiresOnlinePayment).HasColumnName("requires_online_payment").HasDefaultValue(false);
            entity.Property(x => x.ImageUrl).HasColumnName("image_url");
            entity.Property(x => x.ServiceAddress).HasColumnName("service_address");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Ignore(x => x.ProfessionalIds);
            entity.Ignore(x => x.ImageUrls);
            entity.Ignore(x => x.ImageIds);
            entity.Ignore(x => x.Images);
            entity.Ignore(x => x.PartnerAddress);
        });

        modelBuilder.Entity<ServiceImage>(entity =>
        {
            entity.ToTable("service_images");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.Url).HasColumnName("url").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.ServiceId, x.SortOrder });
        });

        modelBuilder.Entity<ServiceProfessional>(entity =>
        {
            entity.ToTable("service_professionals");
            entity.HasKey(x => new { x.ServiceId, x.ProfessionalId });
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.ProfessionalId).HasColumnName("professional_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<ServiceSlot>(entity =>
        {
            entity.ToTable("service_slots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.ServiceId).HasColumnName("service_id");
            entity.Property(x => x.ProfessionalId).HasColumnName("professional_id");
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
            entity.Property(x => x.Priority).HasColumnName("priority");
            entity.Property(x => x.Owner).HasColumnName("owner");
            entity.Property(x => x.NextFollowUpAt).HasColumnName("next_follow_up_at");
            entity.Property(x => x.InternalNote).HasColumnName("internal_note");
            entity.Property(x => x.OutcomeReason).HasColumnName("outcome_reason");
            entity.Property(x => x.Message).HasColumnName("message");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Priority });
            entity.HasIndex(x => new { x.TenantId, x.NextFollowUpAt });
        });

        modelBuilder.Entity<InboxThread>(entity =>
        {
            entity.ToTable("inbox_threads");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.PartnerId).HasColumnName("partner_id");
            entity.Property(x => x.ProfessionalId).HasColumnName("professional_id");
            entity.Property(x => x.Subject).HasColumnName("subject").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").IsRequired().HasDefaultValue("open");
            entity.Property(x => x.LastMessageAt).HasColumnName("last_message_at").HasDefaultValueSql("now()");
            entity.Property(x => x.CustomerLastReadAt).HasColumnName("customer_last_read_at");
            entity.Property(x => x.PartnerLastReadAt).HasColumnName("partner_last_read_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.CustomerId, x.LastMessageAt });
            entity.HasIndex(x => new { x.TenantId, x.PartnerId, x.LastMessageAt });
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
            entity.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId);
            entity.HasOne(x => x.Professional).WithMany().HasForeignKey(x => x.ProfessionalId);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("inbox_messages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ThreadId).HasColumnName("thread_id");
            entity.Property(x => x.SenderRole).HasColumnName("sender_role").IsRequired();
            entity.Property(x => x.Body).HasColumnName("body").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.ThreadId, x.CreatedAt });
            entity.HasOne(x => x.Thread).WithMany(x => x.Messages).HasForeignKey(x => x.ThreadId);
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
            entity.Property(x => x.ProfessionalId).HasColumnName("professional_id");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.StartAt).HasColumnName("start_at");
            entity.Property(x => x.EndAt).HasColumnName("end_at");
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.CancellationPolicy).HasColumnName("cancellation_policy").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.InternalNote).HasColumnName("internal_note");
            entity.Property(x => x.OutcomeReason).HasColumnName("outcome_reason");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.PartnerId, x.StartAt });
            entity.HasIndex(x => new { x.TenantId, x.PartnerId, x.Status });
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.SellerId).HasColumnName("seller_id");
            entity.Property(x => x.OrderId).HasColumnName("order_id");
            entity.Property(x => x.Provider).HasColumnName("provider").HasDefaultValue("transbank");
            entity.Property(x => x.ExternalReference).HasColumnName("external_reference");
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.TransactionAmount).HasColumnName("transaction_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.PaidAmount).HasColumnName("paid_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.StatusDetail).HasColumnName("status_detail");
            entity.Property(x => x.GatewayIntentId).HasColumnName("gateway_intent_id");
            entity.Property(x => x.ProviderToken).HasColumnName("provider_token");
            entity.Property(x => x.MercadoPagoPaymentId).HasColumnName("mercadopago_payment_id");
            entity.Property(x => x.PaymentMethod).HasColumnName("payment_method");
            entity.Property(x => x.LastEventId).HasColumnName("last_event_id");
            entity.Property(x => x.RawResponseJson).HasColumnName("raw_response_json").HasColumnType("jsonb");
            entity.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
            entity.Property(x => x.CorrelationId).HasColumnName("correlation_id");
            entity.Property(x => x.DateApproved).HasColumnName("date_approved");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Provider, x.ExternalReference }).IsUnique();
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.SellerId);
            entity.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("idempotency_key IS NOT NULL");
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

        modelBuilder.Entity<Seller>(entity =>
        {
            entity.ToTable("sellers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Name).HasColumnName("name").IsRequired();
            entity.Property(x => x.Email).HasColumnName("email").IsRequired();
            entity.Property(x => x.TaxId).HasColumnName("tax_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.TenantId, x.Email });
        });

        modelBuilder.Entity<SellerMercadoPagoAccount>(entity =>
        {
            entity.ToTable("seller_mercadopago_accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.SellerId).HasColumnName("seller_id");
            entity.Property(x => x.MpUserId).HasColumnName("mp_user_id");
            entity.Property(x => x.AccessTokenEncrypted).HasColumnName("access_token_encrypted").IsRequired();
            entity.Property(x => x.RefreshTokenEncrypted).HasColumnName("refresh_token_encrypted");
            entity.Property(x => x.TokenExpiresAt).HasColumnName("token_expires_at");
            entity.Property(x => x.Scope).HasColumnName("scope");
            entity.Property(x => x.ConnectionStatus).HasColumnName("connection_status").IsRequired();
            entity.Property(x => x.ConnectedAt).HasColumnName("connected_at");
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.SellerId).IsUnique();
            entity.HasIndex(x => x.MpUserId);
            entity.HasOne(x => x.Seller).WithOne(x => x.MercadoPagoAccount).HasForeignKey<SellerMercadoPagoAccount>(x => x.SellerId);
        });

        modelBuilder.Entity<SellerFeeConfiguration>(entity =>
        {
            entity.ToTable("seller_fee_configurations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.SellerId).HasColumnName("seller_id");
            entity.Property(x => x.FixedFeeAmount).HasColumnName("fixed_fee_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.PercentageFee).HasColumnName("percentage_fee").HasColumnType("numeric(9,4)");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.SellerId).IsUnique();
            entity.HasOne(x => x.Seller).WithOne(x => x.FeeConfiguration).HasForeignKey<SellerFeeConfiguration>(x => x.SellerId);
        });

        modelBuilder.Entity<PaymentFee>(entity =>
        {
            entity.ToTable("payment_fees");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.PaymentId).HasColumnName("payment_id");
            entity.Property(x => x.PlatformFeeAmount).HasColumnName("platform_fee_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.MercadoPagoFeeAmount).HasColumnName("mercadopago_fee_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.NetToSellerAmount).HasColumnName("net_to_seller_amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.PaymentId).IsUnique();
        });

        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("webhook_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Topic).HasColumnName("topic").IsRequired();
            entity.Property(x => x.Action).HasColumnName("action");
            entity.Property(x => x.ResourceId).HasColumnName("resource_id");
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.SignatureValid).HasColumnName("signature_valid");
            entity.Property(x => x.Processed).HasColumnName("processed");
            entity.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            entity.Property(x => x.ErrorMessage).HasColumnName("error_message");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.Topic, x.Action, x.ResourceId, x.CreatedAt });
        });

        modelBuilder.Entity<PaymentStatusHistory>(entity =>
        {
            entity.ToTable("payment_status_history");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.PaymentId).HasColumnName("payment_id");
            entity.Property(x => x.PreviousStatus).HasColumnName("previous_status");
            entity.Property(x => x.NewStatus).HasColumnName("new_status").IsRequired();
            entity.Property(x => x.Detail).HasColumnName("detail");
            entity.Property(x => x.RawPayloadJson).HasColumnName("raw_payload_json").HasColumnType("jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.PaymentId, x.CreatedAt });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Actor).HasColumnName("actor").IsRequired();
            entity.Property(x => x.Action).HasColumnName("action").IsRequired();
            entity.Property(x => x.EntityType).HasColumnName("entity_type").IsRequired();
            entity.Property(x => x.EntityId).HasColumnName("entity_id").IsRequired();
            entity.Property(x => x.DataJson).HasColumnName("data_json").HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => new { x.EntityType, x.EntityId, x.CreatedAt });
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

        modelBuilder.Entity<ProfessionalFollow>(entity =>
        {
            entity.ToTable("professional_follows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.FollowerProfessionalId).HasColumnName("follower_professional_id");
            entity.Property(x => x.FollowedType).HasColumnName("followed_type").HasMaxLength(20).IsRequired();
            entity.Property(x => x.FollowedId).HasColumnName("followed_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.FollowerProfessionalId);
            entity.HasIndex(x => new { x.FollowedType, x.FollowedId });
        });

        modelBuilder.Entity<NotificationOutbox>(entity =>
        {
            entity.ToTable("notification_outbox");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Channel).HasColumnName("channel").IsRequired().HasDefaultValue("email");
            entity.Property(x => x.Kind).HasColumnName("kind").IsRequired();
            entity.Property(x => x.ReferenceId).HasColumnName("reference_id");
            entity.Property(x => x.Recipient).HasColumnName("recipient");
            entity.Property(x => x.Subject).HasColumnName("subject");
            entity.Property(x => x.Body).HasColumnName("body").IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired().HasDefaultValue("pending");
            entity.Property(x => x.Attempts).HasColumnName("attempts").HasDefaultValue(0);
            entity.Property(x => x.MaxAttempts).HasColumnName("max_attempts").HasDefaultValue(5);
            entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").HasDefaultValueSql("now()");
            entity.Property(x => x.LastError).HasColumnName("last_error");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.Property(x => x.SentAt).HasColumnName("sent_at");
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            entity.HasIndex(x => new { x.ReferenceId, x.Kind });
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
