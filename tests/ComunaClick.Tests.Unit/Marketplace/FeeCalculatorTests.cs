using ComunaClick.Api.Modules.Marketplace;
using Xunit;

namespace ComunaClick.Tests.Unit.Marketplace;

public sealed class FeeCalculatorTests
{
    private readonly FeeCalculator _calculator = new();

    [Fact]
    public void Calculate_WithFixedAndPercentageFee_RoundsForClp()
    {
        var result = _calculator.Calculate(10_000m, 399.6m, 12.5m);

        Assert.Equal(10_000m, result.GrossAmount);
        Assert.Equal(400m, result.FixedFeeAmount);
        Assert.Equal(1_250m, result.PercentageFeeAmount);
        Assert.Equal(1_650m, result.TotalPlatformFeeAmount);
        Assert.Equal(8_350m, result.NetToSellerAmount);
    }

    [Fact]
    public void Calculate_WhenFeeWouldExceedGross_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _calculator.Calculate(1_000m, 900m, 20m));

        Assert.Contains("cannot exceed gross amount", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(1000, -1, 0)]
    [InlineData(1000, 0, -0.1)]
    public void Calculate_WhenAnyInputIsNegative_Throws(decimal grossAmount, decimal fixedFeeAmount, decimal percentageFee)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => _calculator.Calculate(grossAmount, fixedFeeAmount, percentageFee));
    }

    [Theory]
    [InlineData(1.4, 1)]
    [InlineData(1.5, 2)]
    [InlineData(1.6, 2)]
    public void RoundClp_UsesAwayFromZero(decimal input, decimal expected)
    {
        Assert.Equal(expected, FeeCalculator.RoundClp(input));
    }
}
