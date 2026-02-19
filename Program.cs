using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RazorIdentity.Configuration;
using RazorIdentity.Data;
using RazorIdentity.Services;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Add services PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaulConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = true; // Requiere confirmación por correo antes de iniciar sesión
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Envío de correo: si EmailSettings está configurado (Host, User, Password) se usa SMTP; si no, solo se registra en consola
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.SectionName));
var emailSection = builder.Configuration.GetSection(EmailSettings.SectionName);
var emailHost = emailSection["Host"];
var emailUser = emailSection["User"];
var emailPassword = emailSection["Password"];
if (!string.IsNullOrWhiteSpace(emailHost) && !string.IsNullOrWhiteSpace(emailUser) && !string.IsNullOrWhiteSpace(emailPassword))
    builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();

// Cliente HTTP para Rit_Api (URL base en appsettings: ApiSettings:BaseUrl)
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.AddHttpClient<IRitApiClient, RitApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:44337";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    // En desarrollo, aceptar certificado HTTPS autofirmado de Rit_Api (localhost)
    if (builder.Environment.IsDevelopment())
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    return handler;
});

var app = builder.Build();

    async Task CrearRolesIniciales(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = new[] { "Super_admin", "Admin", "Usuario", "Usuario_inicial" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseAuthorization();

app.MapRazorPages();


// Invocar la creaci?n de roles iniciales
await CrearRolesIniciales(app);

app.Run();
