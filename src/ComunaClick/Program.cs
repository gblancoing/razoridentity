using ComunaClick.App.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.LocaleService>();
builder.Services.AddScoped<ComunaClick.SharedUI.Services.AuthStateService>();
builder.Services.AddScoped<ComunaClick.Shared.Auth.Interfaces.ITokenStore, ComunaClick.SharedUI.Services.WebTokenStore>();
builder.Services.Configure<ComunaClick.Shared.Http.ApiOptions>(builder.Configuration.GetSection("Api"));
builder.Services.AddHttpClient<ComunaClick.Shared.Auth.Acl.AclAuthClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComunaClick.Shared.Http.ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.AclBaseUrl);
});
builder.Services.AddScoped<ComunaClick.Shared.Auth.Interfaces.IAuthClient>(sp =>
    sp.GetRequiredService<ComunaClick.Shared.Auth.Acl.AclAuthClient>());
builder.Services.AddHttpClient<ComunaClick.Shared.Api.Buyer.BuyerApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComunaClick.Shared.Http.ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
});
builder.Services.AddHttpClient<ComunaClick.Shared.Api.Partner.PartnerApiClient>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ComunaClick.Shared.Http.ApiOptions>>().Value;
    client.BaseAddress = new Uri(options.ApiBaseUrl);
});
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerCatalogService, ComunaClick.SharedUI.Services.Mocks.MockPartnerCatalogService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerBookingService, ComunaClick.SharedUI.Services.Mocks.MockPartnerBookingService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerLeadService, ComunaClick.SharedUI.Services.Mocks.MockPartnerLeadService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerPayoutService, ComunaClick.SharedUI.Services.Mocks.MockPartnerPayoutService>();
builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerNotificationService, ComunaClick.SharedUI.Services.Mocks.MockPartnerNotificationService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(ComunaClick.SharedUI.Pages.Home).Assembly)
    .AddInteractiveServerRenderMode();

app.Run();
