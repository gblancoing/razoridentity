namespace ComunaClick.Api.Integrations.WhatsAppSms;

public interface INotificationProvider
{
    Task SendOrderConfirmationAsync(Guid customerId, Guid orderId);
    Task SendBookingConfirmationAsync(Guid customerId, Guid bookingId);
    Task SendLeadNotificationAsync(Guid professionalId, Guid leadId);
}
