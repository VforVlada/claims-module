using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Tests.ValueObjects;

public class ClaimNumberTests
{
    [Fact]
    public void Create_FormatsYearAndSequenceAsSevenDigitPaddedNumber()
    {
        var claimNumber = ClaimNumber.Create(2026, 42);

        Assert.Equal("CLM-2026-0000042", claimNumber.Value);
    }

    [Theory]
    [InlineData("CLM-2026-0000001")]
    [InlineData("CLM-1999-1234567")]
    [InlineData("CLM-2026-000051")] // legacy 6-digit number from before the 7-digit format — must still load
    public void Constructor_ValidFormat_Succeeds(string value)
    {
        var claimNumber = new ClaimNumber(value);

        Assert.Equal(value, claimNumber.Value);
        Assert.Equal(value, claimNumber.ToString());
    }

    [Theory]
    [InlineData("CLM-26-0000001")]
    [InlineData("CLM-2026-1")]
    [InlineData("2026-0000001")]
    [InlineData("CLM-2026-00001")]
    [InlineData("CLM-2026-00000001")]
    [InlineData("")]
    public void Constructor_InvalidFormat_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => new ClaimNumber(value));
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new ClaimNumber("CLM-2026-0000001");
        var b = new ClaimNumber("CLM-2026-0000001");

        Assert.Equal(a, b);
    }
}
