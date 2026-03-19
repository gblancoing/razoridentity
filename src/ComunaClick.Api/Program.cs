using ComunaClick.Api.Middleware;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Jobs;
using ComunaClick.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpContextAccessor();
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
        policy.RequireAssertion(context => HasAnyRole(context.User, "partner_owner", "tenant_admin", "platform_admin")));
    options.AddPolicy("partner.staff", policy =>
        policy.RequireAssertion(context => HasAnyRole(context.User, "partner_staff", "partner_owner", "tenant_admin", "platform_admin")));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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
