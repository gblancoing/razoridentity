using Microsoft.AspNetCore.Components.WebView.Maui;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.SharedUI.Services;
using ComunaClick.SharedUI.Http;

namespace ComunaClick.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
			});

		builder.Services.AddMauiBlazorWebView();
#if DEBUG
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		// Servicios de UI y sesión
		builder.Services.AddScoped<LocaleService>();
		builder.Services.AddScoped<UserMapLocationService>();
		builder.Services.AddScoped<ApiMediaUrl>();
		builder.Services.AddScoped<SessionTokenHolder>();
		builder.Services.AddScoped<AuthStateService>();
		builder.Services.AddScoped<ApiSessionService>();
		builder.Services.AddScoped<FavoritesLocalStore>();
		builder.Services.AddScoped<CartState>();
		builder.Services.AddScoped<ITokenStore, ComunaClick.Mobile.Services.SecureTokenStore>();

		// Configuración de API (producción)
		var apiOptions = new ComunaClick.Shared.Http.ApiOptions
		{
			ApiBaseUrl = "https://api.comunaclic.cl",
			AclBaseUrl = "https://acl.comunaclic.cl",
			DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			DefaultPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
			DefaultProfessionalId = Guid.Parse("33333333-3333-3333-3333-333333333333")
		};
		builder.Services.AddSingleton(apiOptions);
		builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(apiOptions));

		// HttpClient base (requerido por ScopedApiHttpClientFactory)
		builder.Services.AddHttpClient();

		// ACL Auth Client
		builder.Services.AddHttpClient<ComunaClick.Shared.Auth.Acl.AclAuthClient>((sp, client) =>
		{
			var options = sp.GetRequiredService<ComunaClick.Shared.Http.ApiOptions>();
			client.BaseAddress = new Uri(options.AclBaseUrl.TrimEnd('/') + "/");
		});
		builder.Services.AddScoped<ComunaClick.Shared.Auth.Interfaces.IAuthClient>(sp =>
			sp.GetRequiredService<ComunaClick.Shared.Auth.Acl.AclAuthClient>());

		// API Clients con bearer token autenticado (mismo patrón que Program.cs del web)
		builder.Services.AddScoped<ComunaClick.Shared.Api.Buyer.BuyerApiClient>(sp =>
		{
			var http = ScopedApiHttpClientFactory.CreateClient(sp);
			return new ComunaClick.Shared.Api.Buyer.BuyerApiClient(
				http,
				sp.GetRequiredService<ITokenStore>(),
				sp.GetRequiredService<ComunaClick.Shared.Auth.Interfaces.IAuthClient>());
		});
		builder.Services.AddScoped<ComunaClick.Shared.Api.Partner.PartnerApiClient>(sp =>
		{
			var http = ScopedApiHttpClientFactory.CreateClient(sp);
			return new ComunaClick.Shared.Api.Partner.PartnerApiClient(
				http,
				sp.GetRequiredService<ITokenStore>(),
				sp.GetRequiredService<ComunaClick.Shared.Auth.Interfaces.IAuthClient>());
		});

		// Servicios de partner
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerCatalogService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerCatalogService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerBookingService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerBookingService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerLeadService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerLeadService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerPayoutService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerPayoutService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerNotificationService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerNotificationService>();
		builder.Services.AddScoped<ComunaClick.SharedUI.Services.Partner.PartnerNotificationsBadgeService>();

		return builder.Build();
	}
}
