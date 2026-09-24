using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Reserves.Commands;
using ClaimsModule.Application.Reserves.Validators;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Tests.Reserves.Validators;

public class OpenReserveCommandValidatorTests
{
    private readonly OpenReserveCommandValidator _sut = new();

    [Fact]
    public void Validate_NonRecoveryZeroAmount_FailsWithBRR01()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.IndemnityReserve, 0m));

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-R-01");
    }

    [Fact]
    public void Validate_NonRecoveryNegativeAmount_FailsWithBRR01()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.IndemnityReserve, -100m));

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-R-01");
    }

    [Fact]
    public void Validate_RecoveryReserveNegativeAmount_Succeeds()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.RecoveryReserve, -5000m));

        Assert.True(result.IsValid);
    }

    /// <summary>FRS A.2: a recovery reserve is money expected back, carried as a negative amount.</summary>
    [Fact]
    public void Validate_RecoveryReservePositiveAmount_FailsWithBRR01()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.RecoveryReserve, 5000m));

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-R-01");
    }

    [Fact]
    public void Validate_RecoveryReserveZeroAmount_FailsWithBRR01()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.RecoveryReserve, 0m));

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-R-01");
    }

    /// <summary>Oversized amounts must fail validation rather than overflow the decimal(19,4) money columns as a 500.</summary>
    [Theory]
    [InlineData(ReserveComponentType.IndemnityReserve, 1_000_000_000.01)]
    [InlineData(ReserveComponentType.RecoveryReserve, -1_000_000_000.01)]
    public void Validate_AmountAboveMax_FailsWithMaxAmountMessage(ReserveComponentType type, double amount)
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), type, (decimal)amount));

        Assert.Contains(result.Errors, e => e.ErrorMessage == ReserveLimits.MaxAmountMessage);
    }

    [Fact]
    public void Validate_AmountAtMax_Succeeds()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.IndemnityReserve, ReserveLimits.MaxAmount));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyClaimId_Fails()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.Empty, ReserveComponentType.IndemnityReserve, 100m));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_InvalidCurrencyLength_Fails()
    {
        var result = _sut.Validate(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.IndemnityReserve, 100m, "US"));

        Assert.False(result.IsValid);
    }
}

public class AdjustReserveCommandValidatorTests
{
    private static (TestDbContext Context, Claim Claim, ClaimReserveComponent Indemnity, ClaimReserveComponent Recovery) Seed()
    {
        var context = TestDbContext.Create();
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var indemnity = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "tester");
        var recovery = claim.OpenReserve(ReserveComponentType.RecoveryReserve, new Money(-2000m), ApprovalTier.Auto, "tester");
        context.Claims.Add(claim);
        context.SaveChanges();
        return (context, claim, indemnity, recovery);
    }

    /// <summary>BR-R-01 on the new amount: zero or negative is invalid for an outstanding reserve.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Validate_NonRecoveryNewAmountNotPositive_FailsWithBRR01(decimal amount)
    {
        var (context, claim, indemnity, _) = Seed();
        using var _ = context;

        var result = await new AdjustReserveCommandValidator(context).ValidateAsync(new AdjustReserveCommand(claim.Id, indemnity.Id, amount, "Revised estimate"));

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-R-01");
    }

    [Theory]
    [InlineData(-3000, true)]
    [InlineData(0, false)]
    [InlineData(1000, false)]
    public async Task Validate_RecoveryNewAmount_MustBeNegative(decimal amount, bool expectedValid)
    {
        var (context, claim, _, recovery) = Seed();
        using var _ = context;

        var result = await new AdjustReserveCommandValidator(context).ValidateAsync(new AdjustReserveCommand(claim.Id, recovery.Id, amount, "Salvage revised"));

        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validate_MissingReason_Fails(string reason)
    {
        var (context, claim, indemnity, _) = Seed();
        using var _ = context;

        var result = await new AdjustReserveCommandValidator(context).ValidateAsync(new AdjustReserveCommand(claim.Id, indemnity.Id, 8000m, reason));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AdjustReserveCommand.Reason));
    }

    [Fact]
    public async Task Validate_PositiveNewAmountWithReason_Succeeds()
    {
        var (context, claim, indemnity, _) = Seed();
        using var _ = context;

        var result = await new AdjustReserveCommandValidator(context).ValidateAsync(new AdjustReserveCommand(claim.Id, indemnity.Id, 8000m, "Revised repair estimate"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_NewAmountAboveMax_FailsWithMaxAmountMessage()
    {
        var (context, claim, indemnity, _) = Seed();
        using var _ = context;

        var result = await new AdjustReserveCommandValidator(context).ValidateAsync(new AdjustReserveCommand(claim.Id, indemnity.Id, ReserveLimits.MaxAmount + 1m, "Revised estimate"));

        Assert.Contains(result.Errors, e => e.ErrorMessage == ReserveLimits.MaxAmountMessage);
    }

    [Fact]
    public async Task Validate_EmptyReserveComponentId_Fails()
    {
        using var context = TestDbContext.Create();

        var result = await new AdjustReserveCommandValidator(context).ValidateAsync(new AdjustReserveCommand(Guid.NewGuid(), Guid.Empty, 100m, "Reason"));

        Assert.False(result.IsValid);
    }
}

public class RejectReserveCommandValidatorTests
{
    private readonly RejectReserveCommandValidator _sut = new();

    [Fact]
    public void Validate_MissingReason_Fails()
    {
        var result = _sut.Validate(new RejectReserveCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ""));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ValidCommand_Succeeds()
    {
        var result = _sut.Validate(new RejectReserveCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Not justified"));

        Assert.True(result.IsValid);
    }
}
