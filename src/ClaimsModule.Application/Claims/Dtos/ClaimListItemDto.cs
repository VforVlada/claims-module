using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.Dtos;

public sealed class ClaimListItemDto
{
    public Guid Id { get; init; }

    public string ClaimNumber { get; init; } = string.Empty;

    public string? PolicyNumber { get; init; }

    public string? ClientName { get; init; }

    public DateTimeOffset LossDate { get; init; }

    public string CauseOfLossCode { get; init; } = string.Empty;

    public ClaimStatus Status { get; init; }

    /// <summary>Flagged by the SLA monitoring job: Draft/Open and untouched for 48h+.</summary>
    public bool IsSlaBreached { get; init; }

    public DateTimeOffset? SlaBreachedAt { get; init; }

    public string AssignedHandler { get; init; } = string.Empty;

    public decimal TotalReserve { get; init; }

    public string Currency { get; init; } = "USD";
}
