using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Reserves.Dtos;

public sealed class ReserveHistoryDto
{
    public Guid Id { get; init; }

    public int ChangeSequence { get; init; }

    /// <summary>The change in outstanding reserve (NewAmount − PreviousAmount).</summary>

    public decimal Amount { get; init; }

    public decimal PreviousAmount { get; init; }

    public decimal NewAmount { get; init; }

    public string? ChangeReason { get; init; }

    public string Currency { get; init; } = "USD";

    public ApprovalTier RequiredTier { get; init; }

    public ApprovalStatus ApprovalStatus { get; init; }

    public PostingStatus PostingStatus { get; init; }

    public string RequestedBy { get; init; } = string.Empty;

    public string? DecidedBy { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    public string? RejectionReason { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
