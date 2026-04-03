using ComunaClick.Api.Middleware;
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
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
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
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var issuer = builder.Configuration["Jwt:Issuer"];
        var audience = builder.Configuration["Jwt:Audience"];
        var signingKey = builder.Configuration["Jwt:SigningKey"];

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
            HasAnyRole(context.User, "customer", "tenant_admin", "platform_admin")));
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
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
    var roleClaims = user.FindAll(AuthConstants.ClaimRole).Select(c => c.Value)
        .Concat(user.FindAll(AuthConstants.ClaimRoles).Select(c => c.Value));
    return roleClaims.Contains(role, StringComparer.OrdinalIgnoreCase);
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
