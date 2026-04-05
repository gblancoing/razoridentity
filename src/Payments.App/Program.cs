using Payments.App.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<PaymentDashboardStore>();
builder.Services.AddScoped<PaymentsWebTokenStore>();
builder.Services.AddScoped<PaymentsAuthStateService>();
builder.Services.Configure<PaymentGatewayOptions>(builder.Configuration.GetSection("PaymentsGateway"));
builder.Services.Configure<PaymentsAuthOptions>(builder.Configuration.GetSection("Acl"));
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

app.MapRazorComponents<Payments.App.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
