using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Tests.Entities;

public class ClaimTests
{
    private static Claim CreateClaim() => Claim.Create(
        organizationEntityId: Guid.NewGuid(),
        claimNumber: ClaimNumber.Create(2026, 1),
        policyId: Guid.NewGuid(),
        claimType: ClaimType.Auto,
        assignedHandler: "Hannah Handler",
        lossDate: DateTimeOffset.UtcNow.AddDays(-1),
        lossDescription: "Rear-end collision",
        lossLocation: "NY",
        causeOfLossCodeId: Guid.NewGuid(),
        createdBy: "tester");

    [Fact]
    public void Create_SetsDraftStatusAndRaisesClaimCreatedEvent()
    {
        var claim = CreateClaim();

        Assert.Equal(ClaimStatus.Draft, claim.Status);
        Assert.False(claim.RequiresManagerOverride);
        var domainEvent = Assert.Single(claim.DomainEvents);
        Assert.IsType<ClaimCreatedEvent>(domainEvent);
    }

    [Fact]
    public void AddParty_AddsPartyAndRaisesPartyAddedEvent()
    {
        var claim = CreateClaim();

        var party = claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", "john@example.com", null, "tester");

        Assert.Contains(party, claim.Parties);
        Assert.Contains(claim.DomainEvents, e => e is PartyAddedEvent);
    }

    [Fact]
    public void AddRiskObject_AddsRiskObjectWithoutRaisingDomainEvent()
    {
        var claim = CreateClaim();
        var eventCountBefore = claim.DomainEvents.Count;

        var riskObject = claim.AddRiskObject(AssetType.Vehicle, "2020 Honda Civic", "VIN123");

        Assert.Contains(riskObject, claim.RiskObjects);
        Assert.Equal(eventCountBefore, claim.DomainEvents.Count);
    }

    [Fact]
    public void AddDocument_AddsDocumentAndRaisesDocumentUploadedEvent()
    {
        var claim = CreateClaim();

        var document = claim.AddDocument("photo.jpg", "photo.jpg", "image/jpeg", 1024, "blob/path", "tester");

        Assert.Contains(document, claim.Documents);
        Assert.Contains(claim.DomainEvents, e => e is DocumentUploadedEvent);
    }

    [Fact]
    public void OpenReserve_AddsReserveComponentToClaim()
    {
        var claim = CreateClaim();

        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "tester");

        Assert.Contains(component, claim.ReserveComponents);
        Assert.Equal(claim.Id, component.ClaimId);
    }

    [Theory]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn)]
    public void OpenReserve_ClaimIsClosedOrWithdrawn_ThrowsInvalidReserveOperationException(ClaimStatus status)
    {
        var claim = CreateClaim();
        claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");
        claim.TransitionTo(ClaimStatus.Open, "tester");
        claim.TransitionTo(status, "tester");

        Assert.Throws<InvalidReserveOperationException>(() => claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "tester"));
    }

    [Fact]
    public void TransitionTo_LeavingDraftWithoutClaimant_ThrowsClaimInvariantViolationException()
    {
        var claim = CreateClaim();

        Assert.Throws<ClaimInvariantViolationException>(() => claim.TransitionTo(ClaimStatus.Open, "tester"));
    }

    [Fact]
    public void TransitionTo_LeavingDraftWithClaimant_SucceedsAndRaisesStatusChangedEvent()
    {
        var claim = CreateClaim();
        claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");

        claim.TransitionTo(ClaimStatus.Open, "tester");

        Assert.Equal(ClaimStatus.Open, claim.Status);
        Assert.Contains(claim.DomainEvents, e => e is ClaimStatusChangedEvent changed && changed.OldStatus == ClaimStatus.Draft && changed.NewStatus == ClaimStatus.Open);
    }

    /// <summary>U-W-04 (domain half): a same-status "transition" is rejected and raises no event.</summary>
    [Fact]
    public void TransitionTo_SameStatus_ThrowsClaimInvariantViolationException()
    {
        var claim = CreateClaim();
        claim.ClearDomainEvents();

        Assert.Throws<ClaimInvariantViolationException>(() => claim.TransitionTo(ClaimStatus.Draft, "tester"));
        Assert.Equal(ClaimStatus.Draft, claim.Status);
        Assert.DoesNotContain(claim.DomainEvents, e => e is ClaimStatusChangedEvent);
    }

    /// <summary>§3.5: the SLA job flags a stale claim once; activity on the claim clears the flag.</summary>
    [Fact]
    public void FlagSlaBreach_IsIdempotentAndClearedByActivity()
    {
        var claim = CreateClaim();
        var detectedAt = new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);

        Assert.True(claim.FlagSlaBreach(detectedAt));
        Assert.False(claim.FlagSlaBreach(detectedAt.AddMinutes(15)));
        Assert.True(claim.IsSlaBreached);
        Assert.Equal(detectedAt, claim.SlaBreachedAt);

        claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");

        Assert.False(claim.IsSlaBreached);
        Assert.Null(claim.SlaBreachedAt);
    }

    [Fact]
    public void UpdateAggregateCapFlag_SetsAndClearsFlag()
    {
        var claim = CreateClaim();

        claim.UpdateAggregateCapFlag(true);
        Assert.True(claim.RequiresManagerOverride);

        claim.UpdateAggregateCapFlag(false);
        Assert.False(claim.RequiresManagerOverride);
    }

    /// <summary>BR-C-06 as a domain invariant: Draft → Closed is refused even if the workflow table were to allow it.</summary>
    [Fact]
    public void TransitionTo_DraftToClosed_ThrowsClaimInvariantViolationException()
    {
        var claim = CreateClaim();
        claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");

        Assert.Throws<ClaimInvariantViolationException>(() => claim.TransitionTo(ClaimStatus.Closed, "tester"));
        Assert.Equal(ClaimStatus.Draft, claim.Status);
    }

    /// <summary>Reserve lines follow the claim: closing or withdrawing it closes them, and a closed line refuses changes and approvals.</summary>
    [Theory]
    [InlineData(ClaimStatus.Closed)]
    [InlineData(ClaimStatus.Withdrawn)]
    public void TransitionTo_ClosedOrWithdrawn_ClosesReservesAndBlocksChangesAndApprovals(ClaimStatus terminal)
    {
        var claim = CreateClaim();
        claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");
        claim.TransitionTo(ClaimStatus.Open, "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "tester");
        var pending = component.History.Single();

        claim.TransitionTo(terminal, "tester");

        Assert.Equal(ReserveComponentStatus.Closed, component.Status);
        Assert.Throws<InvalidReserveOperationException>(() => component.SubmitChange(new Money(1000m), ApprovalTier.Auto, "tester"));
        Assert.Throws<InvalidReserveOperationException>(() => component.Approve(pending.Id, "supervisor", DateTimeOffset.UtcNow));

        // Rejecting a change left pending at closure is still allowed: it just withdraws it.
        component.Reject(pending.Id, "supervisor", "Claim closed", DateTimeOffset.UtcNow);
        Assert.Equal(ApprovalStatus.Rejected, component.ApprovalStatus);
    }

    [Fact]
    public void TransitionTo_Reopened_ReopensReserves()
    {
        var claim = CreateClaim();
        claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");
        claim.TransitionTo(ClaimStatus.Open, "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "tester");
        claim.TransitionTo(ClaimStatus.Closed, "tester");

        claim.TransitionTo(ClaimStatus.Reopened, "tester");

        Assert.Equal(ReserveComponentStatus.Open, component.Status);
        component.SubmitChange(new Money(6000m), ApprovalTier.Auto, "tester");
        Assert.Equal(6000m, component.CurrentAmount.Amount);
    }
}
