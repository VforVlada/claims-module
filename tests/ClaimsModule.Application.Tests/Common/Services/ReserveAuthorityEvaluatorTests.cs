using ClaimsModule.Application.Common.Services;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Tests.Common.Services;

public class ReserveAuthorityEvaluatorTests
{
    private readonly ReserveAuthorityEvaluator _sut = new();

    [Theory]
    [InlineData(10_000, ApprovalTier.Auto)]
    [InlineData(1, ApprovalTier.Auto)]
    [InlineData(10_000.01, ApprovalTier.Supervisor)]
    [InlineData(100_000, ApprovalTier.Supervisor)]
    [InlineData(100_000.01, ApprovalTier.Manager)]
    [InlineData(1_000_000, ApprovalTier.Manager)]
    public void EvaluateTier_RoutesToCorrectTierByThreshold(decimal amount, ApprovalTier expectedTier)
    {
        var tier = _sut.EvaluateTier(new Money(amount));

        Assert.Equal(expectedTier, tier);
    }

    /// <summary>U-R-03..07: the testing plan's exact boundaries at DECIMAL(19,4) precision (U-R-01/02, 0 and −1, are validator failures — see ReserveCommandValidatorsTests).</summary>
    [Theory]
    [InlineData("0.0001", ApprovalTier.Auto)]
    [InlineData("10000.0000", ApprovalTier.Auto)]
    [InlineData("10000.0001", ApprovalTier.Supervisor)]
    [InlineData("100000.0000", ApprovalTier.Supervisor)]
    [InlineData("100000.0001", ApprovalTier.Manager)]
    public void EvaluateTier_PlanBoundaryValues(string amount, ApprovalTier expectedTier)
    {
        var tier = _sut.EvaluateTier(new Money(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal(expectedTier, tier);
    }

    [Fact]
    public void EvaluateTier_NegativeAmount_UsesAbsoluteValue()
    {
        var tier = _sut.EvaluateTier(new Money(-50_000m));

        Assert.Equal(ApprovalTier.Supervisor, tier);
    }

    [Theory]
    [InlineData(10_000_000, false)]
    [InlineData(10_000_000.01, true)]
    [InlineData(-10_000_000.01, true)]
    public void ExceedsAggregateCap_ChecksAbsoluteValueAgainstTenMillion(decimal total, bool expected)
    {
        var exceeds = _sut.ExceedsAggregateCap(new Money(total));

        Assert.Equal(expected, exceeds);
    }

    [Theory]
    [InlineData(ApprovalTier.Supervisor, new[] { "Supervisor" }, true)]
    [InlineData(ApprovalTier.Supervisor, new[] { "Manager" }, true)]
    [InlineData(ApprovalTier.Supervisor, new[] { "Handler" }, false)]
    [InlineData(ApprovalTier.Manager, new[] { "Manager" }, true)]
    [InlineData(ApprovalTier.Manager, new[] { "Supervisor" }, false)]
    [InlineData(ApprovalTier.Manager, new[] { "Handler" }, false)]
    [InlineData(ApprovalTier.Auto, new[] { "Manager" }, false)]
    public void CanApprove_RoutesByTierAndRole(ApprovalTier tier, string[] roles, bool expected)
    {
        var canApprove = _sut.CanApprove(tier, roles);

        Assert.Equal(expected, canApprove);
    }
}
