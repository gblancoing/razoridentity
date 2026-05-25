using ComunaClick.Shared.Auth.Interfaces;
using ComunaClick.Shared.Http;
using ComunaClick.SharedUI.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ComunaClick.SharedUI.Http;

public static class ScopedApiHttpClientFactory
{
    public static HttpClient CreateClient(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<ApiOptions>>().Value;
        var authState = sp.GetRequiredService<AuthStateService>();
        var tokenStore = sp.GetRequiredService<ITokenStore>();

        var pipeline = new AuthBearerDelegatingHandler(authState, tokenStore)
        {
            InnerHandler = new SocketsHttpHandler()
        };

        return new HttpClient(pipeline)
        {
            BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/")
        };
    }
}
