using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Insert-only change record for a reserve component: the reserve moved from PreviousAmount
/// to NewAmount (Amount is the difference, which is what the GL posts), for ChangeReason.
/// The amounts are set once at construction and never rewritten; only the status fields are
/// domain-controlled mutations afterwards (they record what happened to this change).
/// </summary>
public sealed class ReserveHistory : BaseEntity
{
    private ReserveHistory() { }

    public Guid ReserveComponentId { get; private set; }

    /// <summary>1-based, increasing per reserve component — backs the GL idempotency key.</summary>
    public int ChangeSequence { get; private set; }

    /// <summary>The change in outstanding reserve: NewAmount − PreviousAmount.</summary>
    public Money Amount { get; private set; }

    /// <summary>The approved balance when this change was submitted.</summary>
    public Money PreviousAmount { get; private set; }

    /// <summary>The reserve amount this change sets once it is (auto-)approved.</summary>
    public Money NewAmount { get; private set; }

    public string? ChangeReason { get; private set; }

    /// <summary>The tier this change needed at submission time — the authority check at Approve() is evaluated against this, not against a tier recomputed later.</summary>
    public ApprovalTier RequiredTier { get; private set; }

    public ApprovalStatus ApprovalStatus { get; private set; }

    public PostingStatus PostingStatus { get; private set; } = PostingStatus.NotPosted;

    public string? PostingJobId { get; private set; }

    public string? RejectionReason { get; private set; }

    public string RequestedBy { get; private set; } = string.Empty;

    public string? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string IdempotencyKey => $"Reserve:{ReserveComponentId}:Change:{ChangeSequence}";

    internal static ReserveHistory Create(
        Guid organizationEntityId,
        Guid reserveComponentId,
        int changeSequence,
        Money previousAmount,
        Money newAmount,
        string? changeReason,
        ApprovalTier requiredTier,
        ApprovalStatus initialStatus,
        string requestedBy) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationEntityId = organizationEntityId,
        ReserveComponentId = reserveComponentId,
        ChangeSequence = changeSequence,
        Amount = newAmount - previousAmount,
        PreviousAmount = previousAmount,
        NewAmount = newAmount,
        ChangeReason = changeReason,
        RequiredTier = requiredTier,
        ApprovalStatus = initialStatus,
        RequestedBy = requestedBy
    };

    internal void Approve(string approvedBy, DateTimeOffset decidedAt)
    {
        if (ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            throw new InvalidReserveOperationException($"Cannot approve a reserve change in status '{ApprovalStatus}'.");
        }

        ApprovalStatus = ApprovalStatus.Approved;
        DecidedBy = approvedBy;
        DecidedAt = decidedAt;
    }

    internal void Reject(string rejectedBy, string reason, DateTimeOffset decidedAt)
    {
        if (ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            throw new InvalidReserveOperationException($"Cannot reject a reserve change in status '{ApprovalStatus}'.");
        }

        ApprovalStatus = ApprovalStatus.Rejected;
        RejectionReason = reason;
        DecidedBy = rejectedBy;
        DecidedAt = decidedAt;
    }

    public void MarkPosted(string postingJobId)
    {
        PostingStatus = PostingStatus.Posted;
        PostingJobId = postingJobId;
    }

    public void MarkPostingFailed(string postingJobId)
    {
        PostingStatus = PostingStatus.Failed;
        PostingJobId = postingJobId;
    }

    public bool IsBalanceContributing => ApprovalStatus is ApprovalStatus.AutoApproved or ApprovalStatus.Approved;
}
