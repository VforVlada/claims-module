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

    /// <summary>Open while the claim is active; see <see cref="ReserveComponentStatus"/>.</summary>
    public ReserveComponentStatus Status { get; private set; } = ReserveComponentStatus.Open;

    /// <summary>
    /// The approval status of the latest change, kept in step with History by every method that
    /// adds or decides one. PendingApproval therefore means "a change is waiting for a decision".
    /// </summary>
    public ApprovalStatus ApprovalStatus { get; private set; }

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
        EnsureOpen();

        if (_history.Any(h => h.ApprovalStatus == ApprovalStatus.PendingApproval))
        {
            throw new InvalidReserveOperationException("This reserve already has a change pending approval. Approve or reject it before submitting another.");
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
        EnsureOpen();

        var history = GetHistoryOrThrow(reserveHistoryId);
        history.Approve(approvedBy, decidedAt);
        RecalculateBalance();

        AddDomainEvent(new ReserveApprovedEvent(Id, ClaimId, history.Id, history.Amount));
    }

    /// <summary>Allowed on a closed line too: rejecting a change left pending at closure just withdraws it.</summary>
    public void Reject(Guid reserveHistoryId, string rejectedBy, string reason, DateTimeOffset decidedAt)
    {
        var history = GetHistoryOrThrow(reserveHistoryId);
        history.Reject(rejectedBy, reason, decidedAt);
        RecalculateBalance();

        AddDomainEvent(new ReserveRejectedEvent(Id, ClaimId, history.Id, reason));
    }

    /// <summary>Called by the owning Claim when it is closed or withdrawn.</summary>
    internal void Close() => Status = ReserveComponentStatus.Closed;

    /// <summary>Called by the owning Claim when it is reopened.</summary>
    internal void Reopen() => Status = ReserveComponentStatus.Open;

    private void EnsureOpen()
    {
        if (Status == ReserveComponentStatus.Closed)
        {
            throw new InvalidReserveOperationException("This reserve is closed because its claim is closed or withdrawn. Reopen the claim to change it.");
        }
    }

    private ReserveHistory GetHistoryOrThrow(Guid reserveHistoryId) =>
        _history.SingleOrDefault(h => h.Id == reserveHistoryId)
        ?? throw new InvalidReserveOperationException("That reserve change does not belong to this reserve. Reload the claim and try again.");

    private void RecalculateBalance()
    {
        var currency = CurrentAmount.Currency;
        CurrentAmount = _history
            .Where(h => h.IsBalanceContributing)
            .Aggregate(Money.Zero(currency), (sum, h) => sum + h.Amount);
        ApprovalStatus = _history.MaxBy(h => h.ChangeSequence)!.ApprovalStatus;
    }
}
