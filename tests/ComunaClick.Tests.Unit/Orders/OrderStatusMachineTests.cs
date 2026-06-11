using ComunaClick.Api.Modules.Orders;
using Xunit;

namespace ComunaClick.Tests.Unit.Orders;

public sealed class OrderStatusMachineTests
{
    [Theory]
    [InlineData("payment_pending", "processing")]
    [InlineData("payment_pending", "paid")]
    [InlineData("payment_pending", "cancelled")]
    [InlineData("processing", "paid")]
    [InlineData("processing", "cancelled")]
    [InlineData("paid", "cancelled")]
    public void CanTransition_AllowsValidTransitions(string from, string to)
    {
        Assert.True(OrderStatusMachine.CanTransition(from, to, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("cancelled", "paid")]
    [InlineData("cancelled", "payment_pending")]
    [InlineData("cancelled", "processing")]
    [InlineData("paid", "payment_pending")]
    [InlineData("paid", "processing")]
    [InlineData("processing", "payment_pending")]
    public void CanTransition_RejectsInvalidTransitions(string from, string to)
    {
        Assert.False(OrderStatusMachine.CanTransition(from, to, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void CanTransition_RejectsUnknownTargetStatus()
    {
        Assert.False(OrderStatusMachine.CanTransition("payment_pending", "totally_made_up", out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void CanTransition_SameStatus_IsAllowedAsNoOp()
    {
        Assert.True(OrderStatusMachine.CanTransition("paid", "paid", out _));
        Assert.True(OrderStatusMachine.CanTransition("cancelled", "cancelled", out _));
    }

    [Fact]
    public void Normalize_MapsSynonyms()
    {
        Assert.Equal(OrderStatusMachine.Paid, OrderStatusMachine.Normalize("APPROVED"));
        Assert.Equal(OrderStatusMachine.Cancelled, OrderStatusMachine.Normalize("Canceled"));
    }

    [Fact]
    public void CanTransition_LegacyUnknownSource_OnlyAllowsCancellation()
    {
        Assert.True(OrderStatusMachine.CanTransition("legacy_status", "cancelled", out _));
        Assert.False(OrderStatusMachine.CanTransition("legacy_status", "paid", out _));
    }
}
