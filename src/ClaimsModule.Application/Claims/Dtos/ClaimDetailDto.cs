using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.Dtos;

public sealed class ClaimDetailDto
{
    public Guid Id { get; init; }

    public string ClaimNumber { get; init; } = string.Empty;

    public Guid? PolicyId { get; init; }

    public string? PolicyNumber { get; init; }

    public string? ClientName { get; init; }

    public ClaimType ClaimType { get; init; }

    public ClaimStatus Status { get; init; }

    /// <summary>Flagged by the SLA monitoring job: Draft/Open and untouched for 48h+.</summary>
    public bool IsSlaBreached { get; init; }

    public DateTimeOffset? SlaBreachedAt { get; init; }

    public string AssignedHandler { get; init; } = string.Empty;

    public bool RequiresManagerOverride { get; init; }

    public LossEventDto LossEvent { get; init; } = new();

    public IReadOnlyCollection<ClaimPartyDto> Parties { get; init; } = [];

    public IReadOnlyCollection<ClaimRiskObjectDto> RiskObjects { get; init; } = [];

    public IReadOnlyCollection<ReserveComponentDto> ReserveComponents { get; init; } = [];

    public DateTimeOffset CreatedAt { get; init; }
}
