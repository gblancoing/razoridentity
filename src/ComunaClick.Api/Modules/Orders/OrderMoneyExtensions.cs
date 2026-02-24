using ComunaClick.Api.Modules.Orders.Contracts;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Common.Types;

namespace ComunaClick.Api.Modules.Orders;

public static class OrderMoneyExtensions
{
    public static string NormalizeCurrency(this OrderCreateRequest request, string fallback = "CLP")
        => string.IsNullOrWhiteSpace(request.Currency) ? fallback : request.Currency.Trim();

    public static Money SubtotalMoney(this Order order) => new(order.Subtotal, order.Currency);
    public static Money DeliveryFeeMoney(this Order order) => new(order.DeliveryFee, order.Currency);
    public static Money TotalMoney(this Order order) => new(order.TotalAmount, order.Currency);
}
