using LedgerFlow.SharedKernel;

namespace LedgerFlow.UnitTests;

public class MoneyTests
{
    [Fact]
    public void Equal_values_have_value_equality()
    {
        var first = Money.FromDecimal(10.25m);
        var second = Money.FromMinorUnits(1025);
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, Money.FromMinorUnits(1026));
        Assert.Equal("USD", first.Currency);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1025)]
    [InlineData(1025)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void Signed_values_round_trip_without_precision_loss(long cents)
    {
        var money = Money.FromMinorUnits(cents);
        Assert.Equal(money, Money.FromDecimal(money.Amount));
    }

    [Fact]
    public void Arithmetic_is_exact_and_does_not_mutate_operands()
    {
        var first = Money.FromDecimal(0.1m);
        var second = Money.FromDecimal(0.2m);
        Assert.Equal(Money.FromDecimal(0.3m), first + second);
        Assert.Equal(Money.FromDecimal(-0.1m), first - second);
        Assert.Equal(10, first.MinorUnits);
        Assert.Equal(20, second.MinorUnits);
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("usd")]
    [InlineData("")]
    [InlineData(null)]
    public void Unsupported_currency_is_rejected(string? currency)
    {
        Assert.Throws<ArgumentException>(() => Money.FromMinorUnits(100, currency!));
    }

    [Fact]
    public void Fractional_cents_are_rejected_without_rounding()
    {
        Assert.Throws<ArgumentException>(() => Money.FromDecimal(1.001m));
        Assert.Throws<ArgumentException>(() => Money.FromDecimal(-1.001m));
        Assert.Equal(Money.FromMinorUnits(100), Money.FromDecimal(1.000m));
    }

    [Fact]
    public void Overflow_is_rejected_instead_of_wrapping()
    {
        Assert.Throws<OverflowException>(() => Money.FromMinorUnits(long.MaxValue) + Money.FromMinorUnits(1));
        Assert.Throws<OverflowException>(() => Money.FromMinorUnits(long.MinValue) - Money.FromMinorUnits(1));
        Assert.Throws<OverflowException>(() => Money.FromDecimal((decimal)long.MaxValue / 100m + 0.01m));
        Assert.Throws<OverflowException>(() => Money.FromDecimal(decimal.MaxValue));
    }
}
