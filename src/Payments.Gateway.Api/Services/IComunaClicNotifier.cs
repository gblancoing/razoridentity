using Payments.Gateway.Api.Persistence.Entities;

namespace Payments.Gateway.Api.Services;

public interface IComunaClicNotifier
{
    Task NotifyPaymentAsync(PaymentIntent intent, string status, string providerEventId, string? rawPayload, CancellationToken cancellationToken);
}
