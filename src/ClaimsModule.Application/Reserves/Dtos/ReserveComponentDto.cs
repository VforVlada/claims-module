using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Reserves.Dtos;

public sealed class ReserveComponentDto
{
    public Guid Id { get; init; }

    public Guid ClaimId { get; init; }

    public ReserveComponentType ComponentType { get; init; }

    public decimal CurrentAmount { get; init; }

    public string Currency { get; init; } = "USD";

    public ReserveComponentStatus Status { get; init; }

    /// <summary>Approval status of the latest change; PendingApproval means a change awaits a decision.</summary>
    public ApprovalStatus ApprovalStatus { get; init; }

    public IReadOnlyCollection<ReserveHistoryDto> History { get; init; } = [];
}
