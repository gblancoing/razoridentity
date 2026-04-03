using Payments.Common.Models;

namespace Payments.Common.Interfaces;

public interface IPaymentProvider
{
    string Name { get; }

    Task<PaymentProviderCreateResponse> CreatePaymentAsync(
        PaymentProviderCreateRequest request,
        CancellationToken cancellationToken = default);
}
