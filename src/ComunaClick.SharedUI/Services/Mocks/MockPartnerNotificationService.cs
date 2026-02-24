using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Notifications;

namespace ComunaClick.SharedUI.Services.Mocks;

public sealed class MockPartnerNotificationService : IPartnerNotificationService
{
    public Task<IReadOnlyList<NotificationDto>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        IReadOnlyList<NotificationDto> items = new List<NotificationDto>
        {
            new(Guid.NewGuid(), "Nuevo pedido", "Tienes un pedido confirmado hace 5 min.", "order", now.AddMinutes(-5), false),
            new(Guid.NewGuid(), "Reserva próxima", "Una reserva inicia en 1 hora.", "booking", now.AddMinutes(-20), false),
            new(Guid.NewGuid(), "Pago procesado", "Tu payout semanal fue emitido.", "payout", now.AddHours(-5), true)
        };

        return Task.FromResult(items);
    }
}
