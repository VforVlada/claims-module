using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Tests.Claims.Mappings;

public class ClaimTotalsMappingTests
{
    /// <summary>
    /// U-R-21 / QRY-02. Convention: a RecoveryReserve is money expected back (subrogation,
    /// salvage), entered and stored as a negative amount, so the claim total is the net incurred
    /// figure — a plain sum, with no sign flipping anywhere downstream.
    /// </summary>
    [Fact]
    public void TotalReserve_NetsRecoveryReserveAgainstOutstandingReserves()
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler",
            DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(8000m), ApprovalTier.Auto, "tester");
        claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(1500m), ApprovalTier.Auto, "tester");
        claim.OpenReserve(ReserveComponentType.RecoveryReserve, new Money(-2000m), ApprovalTier.Auto, "tester");

        var dto = TestMapperFactory.Create().Map<ClaimListItemDto>(claim);

        Assert.Equal(7500m, dto.TotalReserve);
    }

    [Fact]
    public void TotalReserve_ExcludesPendingApprovalAmounts()
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler",
            DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "tester");
        claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(50000m), ApprovalTier.Supervisor, "tester");

        var dto = TestMapperFactory.Create().Map<ClaimListItemDto>(claim);

        Assert.Equal(5000m, dto.TotalReserve);
    }
}
