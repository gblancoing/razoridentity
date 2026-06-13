using ComunaClick.Api.Integrations.Notifications;
using ComunaClick.Api.Modules.Catalog;
using ComunaClick.Api.Persistence.Entities;

namespace ComunaClick.Tests.Unit.TestSupport;

internal sealed class StubStockNotificationService : IStockNotificationService
{
    public Task NotifyQuantityChangedAsync(
        Product product,
        int previousOnHand,
        int newOnHand,
        string reason,
        Guid? referenceId = null,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class FakeEmailSender : IEmailSender
{
    private int _remainingFailures;

    public FakeEmailSender(int failuresBeforeSuccess = 0)
    {
        _remainingFailures = failuresBeforeSuccess;
    }

    public bool IsConfigured { get; set; } = true;
    public bool AlwaysFail { get; set; }
    public int SendAttempts { get; private set; }
    public int SuccessfulSends { get; private set; }
    public List<(string To, string Subject, string Body)> Sent { get; } = new();

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        SendAttempts++;
        if (AlwaysFail || _remainingFailures > 0)
        {
            _remainingFailures--;
            throw new InvalidOperationException("SMTP temporalmente no disponible (fake).");
        }

        SuccessfulSends++;
        Sent.Add((to, subject, body));
        return Task.CompletedTask;
    }
}

internal sealed class FakeWhatsAppSender : IWhatsAppSender
{
    public bool IsConfigured => true;
    public int SendAttempts { get; private set; }

    public Task SendAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        SendAttempts++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingOrderNotificationService : IOrderNotificationService
{
    public int PartnerPaidNotifications { get; private set; }
    public int BuyerOrderNotifications { get; private set; }
    public List<Guid> PartnerPaidOrderIds { get; } = new();

    public Task NotifyPartnerOrderPaidAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        PartnerPaidNotifications++;
        PartnerPaidOrderIds.Add(orderId);
        return Task.CompletedTask;
    }

    public Task NotifyBuyerOrderAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default)
    {
        BuyerOrderNotifications++;
        return Task.CompletedTask;
    }

    public Task NotifyBuyerBookingAsync(Booking booking, Partner partner, Customer customer, string? serviceName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyBuyerPaymentPendingAsync(Order order, Partner partner, Customer customer, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task NotifyBuyerBookingPaymentPendingAsync(Booking booking, Partner partner, Customer customer, string? serviceName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
