using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Payouts;

namespace ComunaClick.SharedUI.Services.Mocks;

public sealed class MockPartnerPayoutService : IPartnerPayoutService
{
    public Task<IReadOnlyList<PayoutDto>> GetPayoutsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        IReadOnlyList<PayoutDto> items = new List<PayoutDto>
        {
            new(Guid.NewGuid(), "Semana 01", 240000m, "Confirmado", now.AddDays(-7)),
            new(Guid.NewGuid(), "Semana 02", 180000m, "En proceso", now.AddDays(-2)),
            new(Guid.NewGuid(), "Semana 03", 312500m, "Programado", now.AddDays(2))
        };

        return Task.FromResult(items);
    }
}
