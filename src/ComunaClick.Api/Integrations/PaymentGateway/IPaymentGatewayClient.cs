namespace ComunaClick.Api.Integrations.PaymentGateway;

public interface IPaymentGatewayClient
{
    Task<CreatePaymentIntentResult> CreateIntentAsync(string externalReference, decimal amount, string returnUrl, string currency);
    Task<PaymentIntentStatusResult> GetIntentStatusAsync(Guid intentId);
}

public sealed record CreatePaymentIntentResult(Guid IntentId, string? RedirectUrl, string Status);
public sealed record PaymentIntentStatusResult(Guid IntentId, string Status);
