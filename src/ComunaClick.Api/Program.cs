using ComunaClick.Api.Configuration;
using ComunaClick.Api.Middleware;
using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Modules.Marketplace;
using ComunaClick.Api.Modules.Admin;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Jobs;
using ComunaClick.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "ComunaClick API (negocio, catálogo, marketplace)",
        Version = "v1",
        Description = "http://localhost:5277 — backend principal de la app."
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddCors(options =>
{
    options.AddPolicy("app", policy =>
        policy.WithOrigins(
                "https://app.comunaclic.cl",
                "https://admin.comunaclic.cl",
                "https://localhost:5001",
                "https://localhost:7221",
                "https://localhost:7224",
                "http://localhost:7224")
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddDbContext<CoreDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("CoreDb")));
builder.Services.AddDbContext<PaymentsDbContext>(options =>
{
    var paymentsConn = builder.Configuration.GetConnectionString("PaymentsDb")
        ?? builder.Configuration.GetConnectionString("CoreDb");
    options.UseNpgsql(paymentsConn);
});
builder.Services.AddHostedService<JobsHostedService>();
builder.Services.AddScoped<SiteContentService>();
builder.Services.Configure<OrderNotificationOptions>(builder.Configuration.GetSection("OrderNotifications"));
builder.Services.Configure<BusinessRulesOptions>(builder.Configuration.GetSection(BusinessRulesOptions.SectionName));
builder.Services.Configure<InventoryOptions>(builder.Configuration.GetSection(InventoryOptions.SectionName));
builder.Services.AddHttpClient();
builder.Services.AddScoped<IOrderNotificationService, OrderNotificationService>();
builder.Services.AddScoped<IProductInventoryService, ProductInventoryService>();
builder.Services.AddScoped<IStockNotificationService, StockNotificationService>();
builder.Services.AddSingleton<MarketplaceMetricsService>();
builder.Services.AddScoped<FeeCalculator>();
builder.Services.Configure<MercadoPagoMarketplaceOptions>(options =>
{
    var section = builder.Configuration.GetSection("Marketplace:MercadoPago");
    section.Bind(options);
    options.ClientId = builder.Configuration["MP_CLIENT_ID"] ?? options.ClientId;
    options.ClientSecret = builder.Configuration["MP_CLIENT_SECRET"] ?? options.ClientSecret;
    options.RedirectUri = builder.Configuration["MP_REDIRECT_URI"] ?? options.RedirectUri;
    options.WebhookSecret = builder.Configuration["MP_WEBHOOK_SECRET"] ?? options.WebhookSecret;
    options.ApiBaseUrl = builder.Configuration["MP_API_BASE_URL"] ?? options.ApiBaseUrl;
    options.AppBaseUrl = builder.Configuration["APP_BASE_URL"] ?? options.AppBaseUrl;
    options.EncryptionKey = builder.Configuration["ENCRYPTION_KEY"] ?? options.EncryptionKey;
});
builder.Services.AddSingleton<ISecretProtector, AesSecretProtector>();
builder.Services.AddScoped<MarketplaceAuditService>();
builder.Services.AddScoped<SellerMarketplaceService>();
builder.Services.AddScoped<MercadoPagoOAuthService>();
builder.Services.AddScoped<MarketplacePaymentService>();
builder.Services.AddScoped<MercadoPagoWebhookService>();
builder.Services.AddHttpClient<MercadoPagoMarketplaceClient>();
builder.Services.AddSingleton<ComunaClick.Api.Modules.Crm.CustomerAvatarStorage>();
builder.Services.AddSingleton<ComunaClick.Api.Modules.Catalog.ServiceImageStorage>();
builder.Services.AddSingleton<ComunaClick.Api.Modules.Catalog.ProductImageStorage>();
builder.Services.AddSingleton<ComunaClick.Api.Modules.Onboarding.StorefrontBannerStorage>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var signingKey = builder.Configuration["Jwt:SigningKey"];

        // Mantener claim types del token (role, sub, email) para políticas buyer.profile.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(signingKey),
            ValidateLifetime = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = string.IsNullOrWhiteSpace(signingKey)
                ? null
                : new SymmetricSecurityKey(ComunaClick.Common.Auth.JwtKey.GetKeyBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("platform.admin", policy =>
        policy.RequireAssertion(context => HasRole(context.User, "platform_admin")));
    options.AddPolicy("tenant.admin", policy =>
        policy.RequireAssertion(context => HasAnyRole(context.User, "tenant_admin", "platform_admin")));
    options.AddPolicy("partner.owner", policy =>
        policy.RequireAssertion(context => HasPartnerSession(context.User) || HasAnyRole(context.User, "partner_owner", "tenant_admin", "platform_admin")));
    options.AddPolicy("partner.staff", policy =>
        policy.RequireAssertion(context => HasPartnerSession(context.User) || HasAnyRole(context.User, "partner_staff", "partner_owner", "tenant_admin", "platform_admin")));
    options.AddPolicy("buyer.customer", policy =>
        policy.RequireAssertion(context =>
            context.User.Identity?.IsAuthenticated == true &&
            !HasPartnerSession(context.User) &&
            HasAnyRole(context.User, "customer", "buyer", "tenant_admin", "platform_admin")));
    // Perfil CRM: cualquier usuario autenticado (el controlador limita por email del token).
    options.AddPolicy("buyer.profile", policy =>
        policy.RequireAuthenticatedUser());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            message = "Too many requests. Please try again later."
        }, cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        CreateFixedWindowLimiter(context, "global", builder.Configuration, "RateLimiting:Global", 180, 60));
    options.AddPolicy("public-read", context =>
        CreateFixedWindowLimiter(context, "public-read", builder.Configuration, "RateLimiting:PublicRead", 90, 60));
    options.AddPolicy("public-write", context =>
        CreateFixedWindowLimiter(context, "public-write", builder.Configuration, "RateLimiting:PublicWrite", 20, 60));
    options.AddPolicy("webhook", context =>
        CreateFixedWindowLimiter(context, "webhook", builder.Configuration, "RateLimiting:Webhook", 120, 60));
});

var app = builder.Build();

try
{
    await DatabaseSchemaBootstrap.ApplyPendingAsync(
        builder.Configuration.GetConnectionString("CoreDb"),
        app.Environment,
        app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Database schema bootstrap failed.");
}

try
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    await ProductInventoryBackfill.EnsureAllProductsHaveInventoryAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Product inventory backfill failed.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "ComunaClick API — Swagger";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ComunaClick API v1");
    });
}
else
{
    app.UseHsts();
}

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});
app.UseCors("app");
app.UseRouting();
app.UseRateLimiter();
app.UseMiddleware<CorrelationIdMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    await next();
});
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool HasAnyRole(ClaimsPrincipal user, params string[] roles)
{
    return roles.Any(role => HasRole(user, role));
}

static bool HasRole(ClaimsPrincipal user, string role)
{
    var roleClaims = GetRoleClaimValues(user);
    return roleClaims.Contains(role, StringComparer.OrdinalIgnoreCase);
}

static IEnumerable<string> GetRoleClaimValues(ClaimsPrincipal user)
{
    return user.FindAll(AuthConstants.ClaimRole).Select(c => c.Value)
        .Concat(user.FindAll(AuthConstants.ClaimRoles).Select(c => c.Value))
        .Concat(user.FindAll(ClaimTypes.Role).Select(c => c.Value))
        .Concat(user.FindAll("roles").Select(c => c.Value));
}

static bool HasPartnerSession(ClaimsPrincipal user)
{
    var partner = user.FindFirst(AuthConstants.ClaimPartnerId)?.Value
        ?? user.FindFirst("partnerId")?.Value
        ?? user.FindFirst("partner_id")?.Value;
    return Guid.TryParse(partner, out var partnerId) && partnerId != Guid.Empty;
}

static RateLimitPartition<string> CreateFixedWindowLimiter(
    HttpContext context,
    string policyName,
    IConfiguration configuration,
    string sectionPath,
    int defaultPermitLimit,
    int defaultWindowSeconds)
{
    var section = configuration.GetSection(sectionPath);
    var permitLimit = Math.Max(1, section.GetValue("PermitLimit", defaultPermitLimit));
    var windowSeconds = Math.Max(1, section.GetValue("WindowSeconds", defaultWindowSeconds));
    var partitionKey = $"{policyName}:{GetRateLimitKey(context)}";

    return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(windowSeconds),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 0,
        AutoReplenishment = true
    });
}

static string GetRateLimitKey(HttpContext context)
{
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? context.User.FindFirst("sub")?.Value;

    if (!string.IsNullOrWhiteSpace(userId))
    {
        return $"user:{userId}";
    }

    return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
}
