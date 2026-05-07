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
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
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
var ritApiBaseUrl = (builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:44337").Trim();
builder.Services.AddHttpClient<IRitApiClient, RitApiClient>(client =>
{
    client.BaseAddress = new Uri(ritApiBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    bool isLocalhost = ritApiBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);
    if (builder.Environment.IsDevelopment() || isLocalhost)
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    return handler;
});

// Cliente HTTP para API_Ritweb (eventos RIT: alertas, inspecciones)
builder.Services.Configure<ApiRitwebSettings>(builder.Configuration.GetSection(ApiRitwebSettings.SectionName));
var apiRitwebBaseUrl = (builder.Configuration[$"{ApiRitwebSettings.SectionName}:BaseUrl"] ?? "https://localhost:7053").Trim();
builder.Services.AddHttpClient<IApiRitwebClient, ApiRitwebClient>(client =>
{
    client.BaseAddress = new Uri(apiRitwebBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    bool isLocalhost = apiRitwebBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);
    if (builder.Environment.IsDevelopment() || isLocalhost)
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    return handler;
});

// Cliente HTTP para API Monte Carlo (simulaciones probabilísticas)
builder.Services.Configure<MontecarloApiSettings>(builder.Configuration.GetSection(MontecarloApiSettings.SectionName));
var montecarloBaseUrl = (builder.Configuration[$"{MontecarloApiSettings.SectionName}:BaseUrl"] ?? "http://localhost:5080").Trim();
builder.Services.AddHttpClient<IMontecarloApiClient, MontecarloApiClient>(client =>
{
    client.BaseAddress = new Uri(montecarloBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    bool isLocalhost = montecarloBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);
    if (builder.Environment.IsDevelopment() || isLocalhost)
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    return handler;
});

// Cliente HTTP para API PMO/PYC (mismo patrón que MontecarloApi: JSON camelCase, errores HTTP con cuerpo, timeout localhost)
builder.Services.Configure<PycApiSettings>(builder.Configuration.GetSection(PycApiSettings.SectionName));
builder.Services.Configure<PycImportSettings>(builder.Configuration.GetSection(PycImportSettings.SectionName));
var pycBaseUrl = (builder.Configuration[$"{PycApiSettings.SectionName}:BaseUrl"] ?? "http://localhost:5299").Trim();
builder.Services.AddHttpClient<IPycApiClient, PycApiClient>(client =>
{
    client.BaseAddress = new Uri(pycBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(60);
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
    bool isLocalhost = pycBaseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase);
    if (builder.Environment.IsDevelopment() || isLocalhost)
        handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
    return handler;
});

builder.Services.AddScoped<PmoFinancePostgresStore>();
builder.Services.AddScoped<PmoReporte1CurvaSEvmService>();
builder.Services.AddScoped<RazorIdentity.Services.PmoFactorialService>();

// Servicio Monte Carlo local (distribución triangular, sin API externa)
builder.Services.AddSingleton<RazorIdentity.Services.MonteCarloService>();

// Servicio MiroFish — motor de enjambre multiagente (requiere servidor Python en localhost:5001)
builder.Services.Configure<RazorIdentity.Configuration.MiroFishSettings>(
    builder.Configuration.GetSection(RazorIdentity.Configuration.MiroFishSettings.SectionName));
var miroFishBaseUrl = (builder.Configuration[$"{RazorIdentity.Configuration.MiroFishSettings.SectionName}:BaseUrl"] ?? "http://localhost:5001").Trim();
var miroFishTimeout = builder.Configuration.GetValue<int>($"{RazorIdentity.Configuration.MiroFishSettings.SectionName}:HttpTimeoutSeconds", 60);
builder.Services.AddHttpClient("MiroFish", client =>
{
    client.BaseAddress = new Uri(miroFishBaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(miroFishTimeout);
});
builder.Services.AddSingleton<RazorIdentity.Services.MiroFishService>();

// Cliente HTTP para Api_Ollama (chat IA)
builder.Services.Configure<OllamaApiSettings>(builder.Configuration.GetSection(OllamaApiSettings.SectionName));
builder.Services.AddHttpClient<IOllamaApiClient, OllamaApiClient>(client =>
{
    var baseUrl = builder.Configuration[$"{OllamaApiSettings.SectionName}:BaseUrl"] ?? "https://localhost:7006";
    var ollamaTimeout = builder.Configuration.GetValue<int>($"{OllamaApiSettings.SectionName}:HttpTimeoutSeconds", 300);
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(Math.Max(30, ollamaTimeout));
}).ConfigurePrimaryHttpMessageHandler(() =>
{
    var handler = new HttpClientHandler();
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

// Permitir que SharePoint incruste la app en un iframe (SharePoint solo como túnel; la lógica y datos siguen en local)
var sharePointOrigins = builder.Configuration["SharePoint:FrameAncestors"]?.Trim();
if (!string.IsNullOrEmpty(sharePointOrigins))
{
    var allowed = string.Join(" ", sharePointOrigins
        .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(o => o.Trim()));
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("Content-Security-Policy", $"frame-ancestors 'self' {allowed}");
        await next();
    });
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Alias corto para informe ejecutivo CODELCO (misma página que resumen con ?v=ejecutivo)
app.MapGet("/TallerCostosPdfEjecutivo/{proyectoId:guid}",
        (Guid proyectoId) => Results.Redirect($"/TallerCostosPdfResumen/{proyectoId}?v=ejecutivo"))
    .RequireAuthorization();

// Invocar la creación de roles iniciales
await CrearRolesIniciales(app);

// Aplicar migraciones pendientes automáticamente
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RazorIdentity.Data.ApplicationDbContext>();
    await db.Database.MigrateAsync();

    // Garantizar tabla TallerFamilias y columna FamiliaId en TallerContratos
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            DO $$ BEGIN
                -- Tabla TallerFamilias
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'TallerFamilias'
                ) THEN
                    CREATE TABLE ""TallerFamilias"" (
                        ""Id""          uuid                     NOT NULL,
                        ""ProyectoId""  uuid                     NOT NULL,
                        ""Nombre""      character varying(150)   NOT NULL,
                        ""Descripcion"" character varying(500)   NULL,
                        ""Orden""       integer                  NOT NULL DEFAULT 1,
                        ""CreatedAt""   timestamp with time zone NOT NULL,
                        ""UpdatedAt""   timestamp with time zone NOT NULL,
                        PRIMARY KEY (""Id""),
                        FOREIGN KEY (""ProyectoId"") REFERENCES ""TallerProyectos"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX ""IX_TallerFamilias_ProyectoId"" ON ""TallerFamilias"" (""ProyectoId"");
                END IF;

                -- Columna FamiliaId en TallerContratos
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'TallerContratos' AND column_name = 'FamiliaId'
                ) THEN
                    ALTER TABLE ""TallerContratos"" ADD COLUMN ""FamiliaId"" uuid NULL;
                    CREATE INDEX ""IX_TallerContratos_FamiliaId"" ON ""TallerContratos"" (""FamiliaId"");
                    ALTER TABLE ""TallerContratos"" ADD CONSTRAINT ""FK_TallerContratos_TallerFamilias_FamiliaId""
                        FOREIGN KEY (""FamiliaId"") REFERENCES ""TallerFamilias"" (""Id"") ON DELETE SET NULL;
                END IF;

                -- Registrar migración en historial si no existe
                IF NOT EXISTS (
                    SELECT 1 FROM ""__EFMigrationsHistory"" WHERE ""MigrationId"" = '20260409000000_AddTallerFamilia'
                ) THEN
                    INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                    VALUES ('20260409000000_AddTallerFamilia', '8.0.0');
                END IF;
            END $$;
        ");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "No se pudo crear tabla TallerFamilias via fallback SQL");
    }

    // Garantizar tabla TallerMcResultados (fallback por si la migración EF no la creó)
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            DO $$ BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_name = 'TallerMcResultados'
                ) THEN
                    CREATE TABLE ""TallerMcResultados"" (
                        ""Id""             uuid                     NOT NULL,
                        ""ContratoId""     uuid                     NOT NULL,
                        ""FechaCalculo""   timestamp with time zone NOT NULL,
                        ""P10""            double precision         NOT NULL DEFAULT 0,
                        ""P50""            double precision         NOT NULL DEFAULT 0,
                        ""P80""            double precision         NOT NULL DEFAULT 0,
                        ""P90""            double precision         NOT NULL DEFAULT 0,
                        ""Media""          double precision         NOT NULL DEFAULT 0,
                        ""DesvStd""        double precision         NOT NULL DEFAULT 0,
                        ""Iteraciones""    integer                  NOT NULL DEFAULT 0,
                        ""ItemMediasJson"" text                     NOT NULL DEFAULT '[]',
                        ""HistogramaJson"" text                     NOT NULL DEFAULT '{}',
                        ""CdfJson""        text                     NOT NULL DEFAULT '[]',
                        PRIMARY KEY (""Id""),
                        FOREIGN KEY (""ContratoId"") REFERENCES ""TallerContratos"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX ""IX_TallerMcResultados_ContratoId""
                        ON ""TallerMcResultados"" (""ContratoId"");
                END IF;
            END $$;
        ");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "No se pudo crear tabla TallerMcResultados via fallback SQL");
    }

    // Garantizar TallerEmpresas + EmpresaId (fallback: migración en historial pero tablas no creadas u otra BD)
    try
    {
        await db.Database.ExecuteSqlRawAsync(@"
            DO $emp$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = 'TallerEmpresas'
                ) THEN
                    CREATE TABLE ""TallerEmpresas"" (
                        ""Id"" uuid NOT NULL,
                        ""ProyectoId"" uuid NOT NULL,
                        ""Rut"" character varying(20) NOT NULL,
                        ""Nombre"" character varying(300) NOT NULL,
                        ""Contacto"" character varying(200) NULL,
                        ""Email"" character varying(200) NULL,
                        ""Telefono"" character varying(50) NULL,
                        ""Notas"" character varying(500) NULL,
                        ""Orden"" integer NOT NULL DEFAULT 1,
                        ""CreatedAt"" timestamp with time zone NOT NULL,
                        ""UpdatedAt"" timestamp with time zone NOT NULL,
                        PRIMARY KEY (""Id""),
                        CONSTRAINT ""FK_TallerEmpresas_TallerProyectos_ProyectoId"" FOREIGN KEY (""ProyectoId"")
                            REFERENCES ""TallerProyectos"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX ""IX_TallerEmpresas_ProyectoId"" ON ""TallerEmpresas"" (""ProyectoId"");
                    CREATE UNIQUE INDEX ""IX_TallerEmpresas_ProyectoId_Rut""
                        ON ""TallerEmpresas"" (""ProyectoId"", ""Rut"");
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'TallerContratos' AND column_name = 'EmpresaId'
                ) THEN
                    ALTER TABLE ""TallerContratos"" ADD COLUMN ""EmpresaId"" uuid NULL;
                    CREATE INDEX ""IX_TallerContratos_EmpresaId"" ON ""TallerContratos"" (""EmpresaId"");
                END IF;

                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TallerContratos_TallerEmpresas_EmpresaId') THEN
                    ALTER TABLE ""TallerContratos"" ADD CONSTRAINT ""FK_TallerContratos_TallerEmpresas_EmpresaId""
                        FOREIGN KEY (""EmpresaId"") REFERENCES ""TallerEmpresas"" (""Id"") ON DELETE SET NULL;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM ""__EFMigrationsHistory""
                    WHERE ""MigrationId"" = '20260423120000_AddTallerEmpresaContratista'
                ) THEN
                    INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                    VALUES ('20260423120000_AddTallerEmpresaContratista', '8.0.0');
                END IF;
            END
            $emp$;
        ");
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "No se pudo asegurar esquema TallerEmpresas / EmpresaId via fallback SQL");
    }
}

app.Run();
