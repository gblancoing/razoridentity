using Payments.App.Models;

namespace Payments.App.Services;

public sealed class PaymentDashboardStore
{
    private readonly IReadOnlyList<PaymentTransactionSummary> _transactions =
    [
        new(
            Guid.Parse("93000000-0000-0000-0000-000000000001"),
            "SUBS-PLUS-0001",
            "transbank",
            "tbk_8e7c1f",
            29990,
            "CLP",
            "captured",
            "AUTH-32891",
            "Partner Mercado Central",
            "Plan ComunaClic Plus",
            DateTimeOffset.UtcNow.AddHours(-4),
            [
                new ProviderEventSummary("webpay.create", "{\"token\":\"tbk_8e7c1f\",\"url\":\"sandbox\"}", DateTimeOffset.UtcNow.AddHours(-4)),
                new ProviderEventSummary("webpay.commit", "{\"response_code\":0,\"status\":\"AUTHORIZED\"}", DateTimeOffset.UtcNow.AddHours(-3.95))
            ]),
        new(
            Guid.Parse("93000000-0000-0000-0000-000000000002"),
            "SUBS-PRO-0002",
            "mercadopago",
            "mp_pref_114",
            49990,
            "CLP",
            "pending",
            null,
            "Estudio Belleza Norte",
            "Plan ComunaClic Pro",
            DateTimeOffset.UtcNow.AddHours(-2),
            [
                new ProviderEventSummary("preference.create", "{\"id\":\"mp_pref_114\",\"status\":\"pending\"}", DateTimeOffset.UtcNow.AddHours(-2))
            ]),
        new(
            Guid.Parse("93000000-0000-0000-0000-000000000003"),
            "SUBS-LOCAL-0003",
            "khipu",
            "khp_5521",
            19990,
            "CLP",
            "failed",
            null,
            "Profesional Salud Integral",
            "Plan Emprendedor Local",
            DateTimeOffset.UtcNow.AddHours(-7),
            [
                new ProviderEventSummary("khipu.create", "{\"payment_id\":\"khp_5521\"}", DateTimeOffset.UtcNow.AddHours(-7)),
                new ProviderEventSummary("khipu.notify", "{\"status\":\"rejected\"}", DateTimeOffset.UtcNow.AddHours(-6.9))
            ]),
        new(
            Guid.Parse("93000000-0000-0000-0000-000000000004"),
            "SUBS-PLUS-0004",
            "transbank",
            "tbk_b2ad74",
            29990,
            "CLP",
            "captured",
            "AUTH-98123",
            "Librería Barrio Vivo",
            "Plan ComunaClic Plus",
            DateTimeOffset.UtcNow.AddDays(-1),
            [
                new ProviderEventSummary("webpay.create", "{\"token\":\"tbk_b2ad74\"}", DateTimeOffset.UtcNow.AddDays(-1)),
                new ProviderEventSummary("webpay.commit", "{\"response_code\":0,\"status\":\"AUTHORIZED\"}", DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(3))
            ])
    ];

    private readonly IReadOnlyList<PaymentSubscriptionSummary> _subscriptions =
    [
        new(
            Guid.Parse("94000000-0000-0000-0000-000000000001"),
            "Plan ComunaClic Plus",
            "transbank",
            "active",
            "Partner Mercado Central",
            "Mercado Central",
            29990,
            "CLP",
            DateTimeOffset.UtcNow.AddDays(24)),
        new(
            Guid.Parse("94000000-0000-0000-0000-000000000002"),
            "Plan ComunaClic Pro",
            "mercadopago",
            "trial",
            "Estudio Belleza Norte",
            "Studio Belleza",
            49990,
            "CLP",
            DateTimeOffset.UtcNow.AddDays(7)),
        new(
            Guid.Parse("94000000-0000-0000-0000-000000000003"),
            "Plan Emprendedor Local",
            "khipu",
            "past_due",
            "Profesional Salud Integral",
            "Consulta Salud Integral",
            19990,
            "CLP",
            DateTimeOffset.UtcNow.AddDays(2)),
        new(
            Guid.Parse("94000000-0000-0000-0000-000000000004"),
            "Plan ComunaClic Plus",
            "transbank",
            "active",
            "Librería Barrio Vivo",
            "Librería Barrio Vivo",
            29990,
            "CLP",
            DateTimeOffset.UtcNow.AddDays(18))
    ];

    public IReadOnlyList<PaymentTransactionSummary> ListTransactions() => _transactions;

    public IReadOnlyList<PaymentSubscriptionSummary> ListSubscriptions() => _subscriptions;

    public PaymentTransactionSummary? GetTransaction(Guid id)
        => _transactions.FirstOrDefault(x => x.Id == id);

    public AdminDashboardSummaryModel GetDashboard()
        => new(
            _transactions.Where(x => string.Equals(x.Status, "captured", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount),
            _transactions.Count(x => string.Equals(x.Status, "captured", StringComparison.OrdinalIgnoreCase)),
            _transactions.Count(x => string.Equals(x.Status, "pending", StringComparison.OrdinalIgnoreCase)),
            _transactions.Count(x => string.Equals(x.Status, "failed", StringComparison.OrdinalIgnoreCase) || string.Equals(x.Status, "canceled", StringComparison.OrdinalIgnoreCase)),
            _subscriptions.Count(x => string.Equals(x.Status, "active", StringComparison.OrdinalIgnoreCase)),
            _transactions
                .GroupBy(x => x.Provider)
                .Select(g => new AdminProviderBreakdownDto(
                    g.Key,
                    g.Count(),
                    g.Where(x => string.Equals(x.Status, "captured", StringComparison.OrdinalIgnoreCase)).Sum(x => x.Amount)))
                .ToList());
}
