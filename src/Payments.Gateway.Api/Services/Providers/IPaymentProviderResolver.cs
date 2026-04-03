using Payments.Common.Interfaces;

namespace Payments.Gateway.Api.Services.Providers;

public interface IPaymentProviderResolver
{
    IPaymentProvider Resolve(string? providerName);
}
