using ComunaClick.Common.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Payments.Common.Interfaces;
using Payments.Gateway.Api.Persistence;
using Payments.Gateway.Api.Services;
using Payments.Gateway.Api.Services.Providers;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentsDb")));
builder.Services.AddHttpClient<IComunaClicNotifier, ComunaClicNotifier>();
builder.Services.Configure<TransbankProviderOptions>(builder.Configuration.GetSection("PaymentProviders:Transbank"));
builder.Services.Configure<KhipuProviderOptions>(builder.Configuration.GetSection("PaymentProviders:Khipu"));
builder.Services.Configure<MercadoPagoProviderOptions>(builder.Configuration.GetSection("PaymentProviders:MercadoPago"));
builder.Services.AddHttpClient<TransbankPaymentProvider>();
builder.Services.AddHttpClient<KhipuPaymentProvider>();
builder.Services.AddHttpClient<MercadoPagoPaymentProvider>();
builder.Services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<TransbankPaymentProvider>());
builder.Services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<KhipuPaymentProvider>());
builder.Services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<MercadoPagoPaymentProvider>());
builder.Services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();
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
                : new SymmetricSecurityKey(JwtKey.GetKeyBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("payments.admin", policy =>
        policy.RequireAssertion(context => HasAnyRole(context.User, "tenant_admin", "platform_admin")));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
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
