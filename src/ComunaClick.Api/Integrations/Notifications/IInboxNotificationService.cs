namespace ComunaClick.Api.Integrations.Notifications;

public interface IInboxNotificationService
{
    Task NotifyNewInboxMessageAsync(Guid threadId, Guid messageId, string senderRole, CancellationToken cancellationToken = default);
}
