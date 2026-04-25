using Microsoft.AspNetCore.Components.WebView.Maui;
using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.Shared.Auth.Stores;
using ComunaClick.SharedUI.Services;

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

		builder.Services.AddScoped<LocaleService>();
		builder.Services.AddScoped<AuthStateService>();
		builder.Services.AddScoped<ITokenStore, ComunaClick.Mobile.Services.SecureTokenStore>();
		builder.Services.AddSingleton(new ComunaClick.Shared.Http.ApiOptions
		{
			ApiBaseUrl = "http://localhost:5277",
			AclBaseUrl = "http://localhost:5135",
			DefaultTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
			DefaultPartnerId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
			DefaultProfessionalId = Guid.Parse("33333333-3333-3333-3333-333333333333")
		});
		builder.Services.AddHttpClient<ComunaClick.Shared.Auth.Acl.AclAuthClient>((sp, client) =>
		{
			var options = sp.GetRequiredService<ComunaClick.Shared.Http.ApiOptions>();
			client.BaseAddress = new Uri(options.AclBaseUrl);
		});
		builder.Services.AddScoped<ComunaClick.Shared.Auth.Interfaces.IAuthClient>(sp =>
			sp.GetRequiredService<ComunaClick.Shared.Auth.Acl.AclAuthClient>());
		builder.Services.AddHttpClient<ComunaClick.Shared.Api.Buyer.BuyerApiClient>((sp, client) =>
		{
			var options = sp.GetRequiredService<ComunaClick.Shared.Http.ApiOptions>();
			client.BaseAddress = new Uri(options.ApiBaseUrl);
		});
		builder.Services.AddHttpClient<ComunaClick.Shared.Api.Partner.PartnerApiClient>((sp, client) =>
		{
			var options = sp.GetRequiredService<ComunaClick.Shared.Http.ApiOptions>();
			client.BaseAddress = new Uri(options.ApiBaseUrl);
		});
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerCatalogService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerCatalogService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerBookingService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerBookingService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerLeadService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerLeadService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerPayoutService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerPayoutService>();
		builder.Services.AddScoped<ComunaClick.Shared.Partner.Interfaces.IPartnerNotificationService, ComunaClick.SharedUI.Services.Partner.PartnerApiPartnerNotificationService>();

		return builder.Build();
	}
}
