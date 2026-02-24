using ComunaClick.Shared.Partner.Payouts;

namespace ComunaClick.Shared.Partner.Interfaces;

public interface IPartnerPayoutService
{
    Task<IReadOnlyList<PayoutDto>> GetPayoutsAsync(CancellationToken cancellationToken = default);
}
