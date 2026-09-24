using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// CurrentAmount is derived, never directly written — it is the sum of ReserveHistory
/// rows whose ApprovalStatus is AutoApproved or Approved, recomputed after every
/// history insert/decision by RecalculateBalance().
/// </summary>
public sealed class ClaimReserveComponent : AggregateRoot
{
    private readonly List<ReserveHistory> _history = [];

    private ClaimReserveComponent() { }

    public Guid ClaimId { get; private set; }

    public ReserveComponentType ComponentType { get; private set; }

    public Money CurrentAmount { get; private set; }

    public IReadOnlyCollection<ReserveHistory> History => _history.AsReadOnly();

    public static ClaimReserveComponent Open(
        Guid organizationEntityId,
        Guid claimId,
        ReserveComponentType componentType,
        Money initialAmount,
        ApprovalTier tier,
        string requestedBy,
        string? reason = null)
    {
        var component = new ClaimReserveComponent
        {
            Id = Guid.NewGuid(),
            OrganizationEntityId = organizationEntityId,
            ClaimId = claimId,
            ComponentType = componentType,
            CurrentAmount = Money.Zero(initialAmount.Currency)
        };

        component.SubmitChange(initialAmount, tier, requestedBy, string.IsNullOrWhiteSpace(reason) ? "Initial reserve" : reason);
        return component;
    }

    /// <summary>
    /// Proposes a new reserve amount (not a delta). The tier is the caller's authority evaluation
    /// of that amount; the history row records previous → new so the change itself is auditable.
    /// </summary>
    public ReserveHistory SubmitChange(Money newAmount, ApprovalTier tier, string requestedBy, string? changeReason = null)
    {
        if (_history.Any(h => h.ApprovalStatus == ApprovalStatus.PendingApproval))
        {
            throw new InvalidReserveOperationException($"Reserve component '{Id}' already has a change pending approval; it must be decided before another can be submitted.");
        }

        // BR-R-01: outstanding reserves are positive; a RecoveryReserve is money expected back,
        // so it is carried as a negative amount (FRS A.2). Zero is neither.
        var validSign = ComponentType == ReserveComponentType.RecoveryReserve ? newAmount.Amount < 0 : newAmount.Amount > 0;
        if (!validSign)
        {
            throw new InvalidReserveOperationException(ComponentType == ReserveComponentType.RecoveryReserve
                ? "A recovery reserve amount must be negative."
                : "A reserve amount must be greater than zero.");
        }

        if (newAmount == CurrentAmount)
        {
            throw new InvalidReserveOperationException("The new reserve amount is the same as the current amount.");
        }

        var initialStatus = tier == ApprovalTier.Auto ? ApprovalStatus.AutoApproved : ApprovalStatus.PendingApproval;
        var nextSequence = _history.Count + 1;

        var history = ReserveHistory.Create(OrganizationEntityId, Id, nextSequence, CurrentAmount, newAmount, changeReason, tier, initialStatus, requestedBy);
        _history.Add(history);
        RecalculateBalance();

        AddDomainEvent(new ReserveSubmittedEvent(Id, ClaimId, history.Id, history.Amount, initialStatus));
        return history;
    }

    public void Approve(Guid reserveHistoryId, string approvedBy, DateTimeOffset decidedAt)
    {
        var history = GetHistoryOrThrow(reserveHistoryId);
        history.Approve(approvedBy, decidedAt);
        RecalculateBalance();

        AddDomainEvent(new ReserveApprovedEvent(Id, ClaimId, history.Id, history.Amount));
    }

    public void Reject(Guid reserveHistoryId, string rejectedBy, string reason, DateTimeOffset decidedAt)
    {
        var history = GetHistoryOrThrow(reserveHistoryId);
        history.Reject(rejectedBy, reason, decidedAt);

        AddDomainEvent(new ReserveRejectedEvent(Id, ClaimId, history.Id, reason));
    }

    private ReserveHistory GetHistoryOrThrow(Guid reserveHistoryId) =>
        _history.SingleOrDefault(h => h.Id == reserveHistoryId)
        ?? throw new InvalidReserveOperationException($"Reserve history '{reserveHistoryId}' does not belong to reserve component '{Id}'.");

    private void RecalculateBalance()
    {
        var currency = CurrentAmount.Currency;
        CurrentAmount = _history
            .Where(h => h.IsBalanceContributing)
            .Aggregate(Money.Zero(currency), (sum, h) => sum + h.Amount);
    }
}
