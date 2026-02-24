using ComunaClick.Api.Modules.Payments.Contracts;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Types;

namespace ComunaClick.Api.Modules.Payments;

public static class PaymentMoneyExtensions
{
    public static string NormalizeCurrency(this PaymentCreateRequest request, string fallback = "CLP")
        => string.IsNullOrWhiteSpace(request.Currency) ? fallback : request.Currency.Trim();

    public static Money ToMoney(this PaymentCreateRequest request, string fallback = "CLP")
        => new(request.Amount, request.NormalizeCurrency(fallback));

    public static Money ToMoney(this Payment payment)
        => new(payment.Amount, payment.Currency);
}
