namespace ComunaClick.Api.Integrations.WhatsAppSms;

public sealed class DummyNotificationProvider : INotificationProvider
{
    public Task SendOrderConfirmationAsync(Guid customerId, Guid orderId) => Task.CompletedTask;
    public Task SendBookingConfirmationAsync(Guid customerId, Guid bookingId) => Task.CompletedTask;
    public Task SendLeadNotificationAsync(Guid professionalId, Guid leadId) => Task.CompletedTask;
}
