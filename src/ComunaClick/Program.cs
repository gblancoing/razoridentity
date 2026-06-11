using ComunaClick.App.Components;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 6 * 1024 * 1024);
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "__Host-comunaclic-antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});
builder.Services.AddScoped<ComunaClick.SharedUI.Services.LocaleService>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.UserMapLocationService>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.ApiMediaUrl>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.SessionTokenHolder>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.AuthStateService>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.ApiSessionService>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.FavoritesLocalStore>();
builder.Services.AddScoped<ComunaClick.Shared.Auth.Interfaces.ITokenStore, ComunaClick.SharedUI.Services.WebTokenStore>();
builder.Services.Configure<ComunaClick.Shared.Http.ApiOptions>(builder.Configuration.GetSection("Api"));
builder.Services.AddHttpClient<ComunaClick.Shared.Auth.Acl.AclAuthClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComunaClick.Shared.Http.ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.AclBaseUrl);
});
builder.Services.AddHttpClient<ComunaClick.Shared.Auth.Acl.AclAuthClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComunaClick.Shared.Http.ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.AclBaseUrl.TrimEnd('/') + "/");
});
builder.Services.AddScoped<ComunaClick.Shared.Auth.Interfaces.IAuthClient>(sp =>
    sp.GetRequiredService<ComunaClick.Shared.Auth.Acl.AclAuthClient>());
builder.Services.AddScoped<ComunaClick.Shared.Api.Buyer.BuyerApiClient>(sp =>
{
    var http = ComunaClick.SharedUI.Http.ScopedApiHttpClientFactory.CreateClient(sp);
    return new ComunaClick.Shared.Api.Buyer.BuyerApiClient(
        http,
        sp.GetRequiredService<ComunaClick.Shared.Auth.Interfaces.ITokenStore>(),
        sp.GetRequiredService<ComunaClick.Shared.Auth.Interfaces.IAuthClient>());
});
builder.Services.AddScoped<ComunaClick.Shared.Api.Partner.PartnerApiClient>(sp =>
{
    var http = ComunaClick.SharedUI.Http.ScopedApiHttpClientFactory.CreateClient(sp);
    return new ComunaClick.Shared.Api.Partner.PartnerApiClient(
        http,
        sp.GetRequiredService<ComunaClick.Shared.Auth.Interfaces.ITokenStore>(),
        sp.GetRequiredService<ComunaClick.Shared.Auth.Interfaces.IAuthClient>());
});
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerCatalogService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerCatalogService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerBookingService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerBookingService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerLeadService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerLeadService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerPayoutService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerPayoutService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerNotificationService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerNotificationService>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.Partner.PartnerNotificationsBadgeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseCookiePolicy(new CookiePolicyOptions
{
    HttpOnly = HttpOnlyPolicy.Always,
    Secure = CookieSecurePolicy.Always,
    MinimumSameSitePolicy = SameSiteMode.Lax
});
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(self)";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-site";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'self'; " +
        "object-src 'none'; " +
        (app.Environment.IsDevelopment()
            ? "img-src 'self' data: https: http://localhost:5277 http://127.0.0.1:5277 https://*.tile.openstreetmap.org; "
            : "img-src 'self' data: https: https://*.tile.openstreetmap.org; ") +
        "font-src 'self' data: https://fonts.gstatic.com https://fonts.googleapis.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://unpkg.com; " +
        "script-src 'self' 'unsafe-inline' https://cdn.tailwindcss.com https://www.google.com https://www.gstatic.com https://unpkg.com; " +
        "worker-src 'self' blob:; " +
        "media-src 'self' https://d8j0ntlcm91z4.cloudfront.net https://*.cloudfront.net blob:; " +
        (app.Environment.IsDevelopment()
            // Dev: API, Blazor (wss), geocoding, recaptcha + VS Browser Link / Hot Reload (ws/http en localhost).
            ? "connect-src 'self' " +
              "http://localhost:5277 http://localhost:5135 https://localhost:7224 " +
              "http://127.0.0.1:* http://localhost:* ws://127.0.0.1:* ws://localhost:* " +
              "wss://127.0.0.1:* wss://localhost:* wss: " +
              "https://nominatim.openstreetmap.org " +
              "https://prod.spline.design https://draft.spline.design https://unpkg.com " +
              "https://www.google.com https://www.gstatic.com https://recaptcha.google.com https://www.recaptcha.net; "
            : "connect-src 'self' https://api.comunaclic.cl https://acl.comunaclic.cl https://payments.comunaclic.cl https://nominatim.openstreetmap.org https://unpkg.com https://prod.spline.design https://draft.spline.design https://*.spline.design https://*.tile.openstreetmap.org https://www.google.com https://www.gstatic.com https://recaptcha.google.com https://www.recaptcha.net wss:; ") +
        "frame-src 'self' https://www.google.com https://recaptcha.google.com https://www.recaptcha.net; " +
        "upgrade-insecure-requests";
    await next();
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ComunaClick.SharedUI.Pages.Home).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
