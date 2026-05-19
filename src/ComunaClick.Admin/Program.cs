using ComunaClick.Admin.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddSingleton<PaymentDashboardStore>();
builder.Services.AddScoped<PaymentsWebTokenStore>();
builder.Services.AddScoped<PaymentsAuthStateService>();
builder.Services.Configure<AdminApiOptions>(builder.Configuration.GetSection("AdminApi"));
builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection("PaymentGateway"));
builder.Services.Configure<PaymentsAuthOptions>(builder.Configuration.GetSection("Acl"));
builder.Services.AddHttpClient<AdminApiClient>();
builder.Services.AddHttpClient<AdminAclManagementClient>();
builder.Services.AddHttpClient<PaymentGatewayAdminClient>();
builder.Services.AddHttpClient<PaymentsAclAuthClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ComunaClick.Admin.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
