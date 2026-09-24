using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.ReferenceData.Dtos;

public sealed class ClaimStatusDto
{
    public ClaimStatus Status { get; init; }

    public IReadOnlyCollection<ClaimStatus> AllowedNextStatuses { get; init; } = [];
}
