using Payments.Common.Interfaces;

namespace Payments.Gateway.Api.Services.Providers;

public sealed class PaymentProviderResolver : IPaymentProviderResolver
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;

    public PaymentProviderResolver(IEnumerable<IPaymentProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.Name,
            provider => provider,
            StringComparer.OrdinalIgnoreCase);
    }

    public IPaymentProvider Resolve(string? providerName)
    {
        var key = string.IsNullOrWhiteSpace(providerName) ? "transbank" : providerName.Trim();
        if (_providers.TryGetValue(key, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"Unsupported payment provider '{key}'.");
    }
}
