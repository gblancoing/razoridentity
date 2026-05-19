namespace ComunaClick.Api.Modules.Marketplace;

public sealed class FeeCalculator
{
    public FeeCalculationResult Calculate(decimal grossAmount, decimal fixedFeeAmount, decimal percentageFee)
    {
        if (grossAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(grossAmount), "Gross amount cannot be negative.");
        }

        if (fixedFeeAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fixedFeeAmount), "Fixed fee cannot be negative.");
        }

        if (percentageFee < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(percentageFee), "Percentage fee cannot be negative.");
        }

        var percentageComponent = RoundClp(grossAmount * percentageFee / 100m);
        var fixedComponent = RoundClp(fixedFeeAmount);
        var totalFee = RoundClp(fixedComponent + percentageComponent);

        if (totalFee > RoundClp(grossAmount))
        {
            throw new InvalidOperationException("Platform fee cannot exceed gross amount.");
        }

        var netAmount = RoundClp(grossAmount - totalFee);
        return new FeeCalculationResult(grossAmount, fixedComponent, percentageComponent, totalFee, netAmount);
    }

    public static decimal RoundClp(decimal amount)
        => decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}

public sealed record FeeCalculationResult(
    decimal GrossAmount,
    decimal FixedFeeAmount,
    decimal PercentageFeeAmount,
    decimal TotalPlatformFeeAmount,
    decimal NetToSellerAmount);
