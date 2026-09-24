using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>Carries the allowed next statuses (from the seeded table) so the 409 response can list them.</summary>
public sealed class InvalidClaimStatusTransitionException(ClaimStatus from, ClaimStatus to, IReadOnlyCollection<ClaimStatus> allowedNextStatuses)
    : Exception($"Transition from '{from}' to '{to}' is not allowed for the current user's role.")
{
    public ClaimStatus From { get; } = from;

    public ClaimStatus To { get; } = to;

    public IReadOnlyCollection<ClaimStatus> AllowedNextStatuses { get; } = allowedNextStatuses;
}
