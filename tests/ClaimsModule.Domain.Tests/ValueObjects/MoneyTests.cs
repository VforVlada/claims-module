using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_NormalizesCurrencyToUpperInvariant()
    {
        var money = new Money(10m, "usd");

        Assert.Equal("USD", money.Currency);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_MissingCurrency_ThrowsArgumentException(string? currency)
    {
        Assert.Throws<ArgumentException>(() => new Money(10m, currency!));
    }

    [Fact]
    public void Zero_ReturnsZeroAmountWithGivenCurrency()
    {
        var money = Money.Zero("EUR");

        Assert.Equal(0m, money.Amount);
        Assert.Equal("EUR", money.Currency);
    }

    [Fact]
    public void Addition_SameCurrency_SumsAmounts()
    {
        var result = new Money(10m, "USD") + new Money(5m, "USD");

        Assert.Equal(15m, result.Amount);
    }

    [Fact]
    public void Addition_MismatchedCurrency_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => new Money(10m, "USD") + new Money(5m, "EUR"));
    }

    [Fact]
    public void Subtraction_SameCurrency_SubtractsAmounts()
    {
        var result = new Money(10m, "USD") - new Money(4m, "USD");

        Assert.Equal(6m, result.Amount);
    }

    [Theory]
    [InlineData(10, 5, true, false)]
    [InlineData(5, 10, false, true)]
    [InlineData(5, 5, false, false)]
    public void ComparisonOperators_SameCurrency_CompareAmounts(decimal left, decimal right, bool expectedGreater, bool expectedLess)
    {
        var a = new Money(left, "USD");
        var b = new Money(right, "USD");

        Assert.Equal(expectedGreater, a > b);
        Assert.Equal(expectedLess, a < b);
    }

    [Fact]
    public void RecoveryReserve_CanBeNegative()
    {
        var money = new Money(-500m, "USD");

        Assert.Equal(-500m, money.Amount);
    }
}
