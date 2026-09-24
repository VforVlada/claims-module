using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Tests.Entities;

public class ClaimReserveComponentTests
{
    private static readonly DateTimeOffset DecidedAt = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Open_WithAutoTier_AutoApprovesAndCurrentAmountReflectsInitialAmount()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        var history = Assert.Single(component.History);
        Assert.Equal(ApprovalStatus.AutoApproved, history.ApprovalStatus);
        Assert.Equal(5000m, component.CurrentAmount.Amount);
        Assert.Equal(1, history.ChangeSequence);
    }

    [Fact]
    public void Open_WithSupervisorTier_PendingApprovalDoesNotContributeToBalance()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");

        var history = Assert.Single(component.History);
        Assert.Equal(ApprovalStatus.PendingApproval, history.ApprovalStatus);
        Assert.Equal(0m, component.CurrentAmount.Amount);
    }

    [Fact]
    public void Open_RaisesReserveSubmittedEvent()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        Assert.Contains(component.DomainEvents, e => e is ReserveSubmittedEvent);
    }

    [Fact]
    public void SubmitChange_IncrementsChangeSequence()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        var second = component.SubmitChange(new Money(6000m), ApprovalTier.Auto, "handler", "Revised estimate");

        Assert.Equal(2, second.ChangeSequence);
        Assert.Equal(6000m, component.CurrentAmount.Amount);
    }

    /// <summary>History records the move from the previous to the new amount, the change between them, and why.</summary>
    [Fact]
    public void SubmitChange_RecordsPreviousNewChangeAndReason()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        var change = component.SubmitChange(new Money(3500m), ApprovalTier.Auto, "handler", "Repair came in under estimate");

        Assert.Equal(5000m, change.PreviousAmount.Amount);
        Assert.Equal(3500m, change.NewAmount.Amount);
        Assert.Equal(-1500m, change.Amount.Amount);
        Assert.Equal("Repair came in under estimate", change.ChangeReason);
        Assert.Equal("Initial reserve", component.History.First().ChangeReason);
        Assert.Equal(3500m, component.CurrentAmount.Amount);
    }

    /// <summary>BR-R-01 in the domain: outstanding reserves are positive, recovery reserves negative (FRS A.2).</summary>
    [Theory]
    [InlineData(ReserveComponentType.IndemnityReserve, 0)]
    [InlineData(ReserveComponentType.ExpenseReserve, -10)]
    [InlineData(ReserveComponentType.RecoveryReserve, 0)]
    [InlineData(ReserveComponentType.RecoveryReserve, 500)]
    public void Open_AmountWithWrongSignForType_ThrowsInvalidReserveOperationException(ReserveComponentType type, decimal amount) =>
        Assert.Throws<InvalidReserveOperationException>(() =>
            ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), type, new Money(amount), ApprovalTier.Auto, "handler"));

    [Fact]
    public void SubmitChange_SameAsCurrentAmount_ThrowsInvalidReserveOperationException()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        Assert.Throws<InvalidReserveOperationException>(() => component.SubmitChange(new Money(5000m), ApprovalTier.Auto, "handler", "No-op"));
    }

    [Fact]
    public void Approve_PendingHistory_UpdatesStatusAndRecalculatesBalance()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");
        var historyId = component.History.Single().Id;

        component.Approve(historyId, "supervisor", DecidedAt);

        Assert.Equal(ApprovalStatus.Approved, component.History.Single().ApprovalStatus);
        Assert.Equal(DecidedAt, component.History.Single().DecidedAt);
        Assert.Equal(50000m, component.CurrentAmount.Amount);
        Assert.Contains(component.DomainEvents, e => e is ReserveApprovedEvent);
    }

    [Fact]
    public void Approve_AlreadyApprovedHistory_ThrowsInvalidReserveOperationException()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");
        var historyId = component.History.Single().Id;
        component.Approve(historyId, "supervisor", DecidedAt);

        Assert.Throws<InvalidReserveOperationException>(() => component.Approve(historyId, "supervisor", DecidedAt));
    }

    [Fact]
    public void Reject_PendingHistory_UpdatesStatusAndDoesNotContributeToBalance()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");
        var historyId = component.History.Single().Id;

        component.Reject(historyId, "supervisor", "Not justified", DecidedAt);

        Assert.Equal(ApprovalStatus.Rejected, component.History.Single().ApprovalStatus);
        Assert.Equal("Not justified", component.History.Single().RejectionReason);
        Assert.Equal(0m, component.CurrentAmount.Amount);
        Assert.Contains(component.DomainEvents, e => e is ReserveRejectedEvent);
    }

    [Fact]
    public void Reject_AlreadyDecidedHistory_ThrowsInvalidReserveOperationException()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");
        var historyId = component.History.Single().Id;
        component.Reject(historyId, "supervisor", "Not justified", DecidedAt);

        Assert.Throws<InvalidReserveOperationException>(() => component.Reject(historyId, "supervisor", "Again", DecidedAt));
    }

    [Fact]
    public void ResubmissionAfterRejection_OriginalStaysRejectedAndNewHistoryCreated()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");
        var firstHistoryId = component.History.Single().Id;
        component.Reject(firstHistoryId, "supervisor", "Not justified", DecidedAt);

        var resubmission = component.SubmitChange(new Money(45000m), ApprovalTier.Supervisor, "handler");

        Assert.Equal(2, component.History.Count);
        Assert.Equal(ApprovalStatus.Rejected, component.History.Single(h => h.Id == firstHistoryId).ApprovalStatus);
        Assert.Equal(ApprovalStatus.PendingApproval, resubmission.ApprovalStatus);
        Assert.Equal(2, resubmission.ChangeSequence);
    }

    [Fact]
    public void Approve_UnknownHistoryId_ThrowsInvalidReserveOperationException()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        Assert.Throws<InvalidReserveOperationException>(() => component.Approve(Guid.NewGuid(), "supervisor", DecidedAt));
    }

    [Fact]
    public void IdempotencyKey_IsDerivedFromComponentIdAndChangeSequence()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");
        var history = component.History.Single();

        Assert.Equal($"Reserve:{component.Id}:Change:1", history.IdempotencyKey);
    }

    /// <summary>Regression: history rows were saved without a tenant, which the tenant query filter then hid from everyone.</summary>
    [Fact]
    public void SubmitChange_HistoryInheritsComponentOrganization()
    {
        var organizationId = Guid.NewGuid();
        var component = ClaimReserveComponent.Open(organizationId, Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        component.SubmitChange(new Money(1000m), ApprovalTier.Auto, "handler");

        Assert.All(component.History, h => Assert.Equal(organizationId, h.OrganizationEntityId));
    }

    [Fact]
    public void SubmitChange_StoresRequiredTierOnHistory()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");

        Assert.Equal(ApprovalTier.Supervisor, component.History.Single().RequiredTier);
    }

    [Fact]
    public void SubmitChange_WhilePriorChangeStillPendingApproval_ThrowsInvalidReserveOperationException()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "handler");

        Assert.Throws<InvalidReserveOperationException>(() => component.SubmitChange(new Money(1000m), ApprovalTier.Auto, "handler"));
    }

    /// <summary>The component's ApprovalStatus is always the status of its latest change.</summary>
    [Fact]
    public void ApprovalStatus_FollowsTheLatestChangeThroughSubmitApproveAndReject()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");
        Assert.Equal(ApprovalStatus.AutoApproved, component.ApprovalStatus);

        var pending = component.SubmitChange(new Money(50000m), ApprovalTier.Supervisor, "handler");
        Assert.Equal(ApprovalStatus.PendingApproval, component.ApprovalStatus);

        component.Reject(pending.Id, "supervisor", "Not justified", DecidedAt);
        Assert.Equal(ApprovalStatus.Rejected, component.ApprovalStatus);

        var resubmitted = component.SubmitChange(new Money(40000m), ApprovalTier.Supervisor, "handler");
        component.Approve(resubmitted.Id, "supervisor", DecidedAt);
        Assert.Equal(ApprovalStatus.Approved, component.ApprovalStatus);
    }

    [Fact]
    public void Open_NewComponentIsOpen()
    {
        var component = ClaimReserveComponent.Open(Guid.NewGuid(), Guid.NewGuid(), ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "handler");

        Assert.Equal(ReserveComponentStatus.Open, component.Status);
    }
}
