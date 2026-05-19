using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Payouts;
using ComunaClick.SharedUI.Services;

namespace ComunaClick.SharedUI.Services.Partner;

public sealed class PartnerPayoutService : PartnerServiceBase, IPartnerPayoutService
{
    public PartnerPayoutService(ComunaClick.Shared.Api.Partner.PartnerApiClient partnerApi, AuthStateService authState)
        : base(partnerApi, authState)
    {
    }

    public async Task<IReadOnlyList<PayoutDto>> GetPayoutsAsync(CancellationToken cancellationToken = default)
    {
        var partnerId = await ResolvePartnerIdAsync(cancellationToken);
        if (!partnerId.HasValue)
        {
            return Array.Empty<PayoutDto>();
        }

        var payouts = await PartnerApi.GetPartnerPayoutsAsync(partnerId.Value, cancellationToken: cancellationToken) ?? [];
        return payouts
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PayoutDto(
                x.Id,
                BuildPeriodLabel(x.CreatedAt),
                Convert.ToDecimal(x.NetAmount),
                x.BatchStatus ?? "open",
                x.CreatedAt))
            .ToList();
    }

    private static string BuildPeriodLabel(DateTimeOffset createdAt)
        => $"Semana {System.Globalization.ISOWeek.GetWeekOfYear(createdAt.UtcDateTime):00}";
}
