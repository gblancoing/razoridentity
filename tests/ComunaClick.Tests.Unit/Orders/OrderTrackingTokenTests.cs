using ComunaClick.Api.Modules.Orders;
using Microsoft.Extensions.Options;
using Xunit;

namespace ComunaClick.Tests.Unit.Orders;

public sealed class OrderTrackingTokenTests
{
    private const string Secret = "unit-test-tracking-secret-0123456789-abcdef";

    // H9: un token válido permite resolver la orden y su customerId.
    [Fact]
    public void ValidToken_RoundTrips()
    {
        var service = Create();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var token = service.Create(orderId, customerId);

        Assert.True(service.TryValidate(token, orderId, out var resolved));
        Assert.Equal(customerId, resolved);
    }

    // H9: sin token (o token vacío) no se accede.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("aaa.bbb")]
    public void InvalidToken_IsRejected(string? token)
    {
        var service = Create();
        Assert.False(service.TryValidate(token, Guid.NewGuid(), out _));
    }

    // H9: un token firmado para otra orden no sirve.
    [Fact]
    public void Token_ForDifferentOrder_IsRejected()
    {
        var service = Create();
        var token = service.Create(Guid.NewGuid(), Guid.NewGuid());
        Assert.False(service.TryValidate(token, Guid.NewGuid(), out _));
    }

    // H9: un token manipulado (firma inválida) se rechaza.
    [Fact]
    public void TamperedToken_IsRejected()
    {
        var service = Create();
        var orderId = Guid.NewGuid();
        var token = service.Create(orderId, Guid.NewGuid());
        var tampered = token[..^2] + (token.EndsWith("aa") ? "bb" : "aa");

        Assert.False(service.TryValidate(tampered, orderId, out _));
    }

    // H9: el token caduca.
    [Fact]
    public void ExpiredToken_IsRejected()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = new OrderTrackingTokenService(
            Options.Create(new OrderTrackingOptions { TrackingTokenSecret = Secret, TrackingTokenTtlDays = 1 }),
            clock);

        var orderId = Guid.NewGuid();
        var token = service.Create(orderId, Guid.NewGuid());

        clock.Advance(TimeSpan.FromDays(2));
        Assert.False(service.TryValidate(token, orderId, out _));
    }

    private static OrderTrackingTokenService Create()
        => new(
            Options.Create(new OrderTrackingOptions { TrackingTokenSecret = Secret, TrackingTokenTtlDays = 30 }),
            TimeProvider.System);

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public MutableTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }
}
