using ComunaClick.Api.Middleware;

namespace ComunaClick.Api.Modules.Marketplace;

public static class MarketplaceHttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext context)
        => context.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;
}
