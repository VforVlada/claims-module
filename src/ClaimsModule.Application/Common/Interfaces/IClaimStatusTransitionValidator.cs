using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Validates a status transition against the seeded, data-driven ClaimStatusTransitions
/// table (BR-C-06) — no hardcoded switch statement.
/// </summary>
public interface IClaimStatusTransitionValidator
{
    Task<TransitionCheck> CheckAsync(ClaimStatus from, ClaimStatus to, IReadOnlyCollection<string> userRoles, CancellationToken cancellationToken);

    Task<bool> IsAllowedAsync(ClaimStatus from, ClaimStatus to, IReadOnlyCollection<string> userRoles, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ClaimStatus>> GetAllowedNextStatusesAsync(ClaimStatus from, IReadOnlyCollection<string> userRoles, CancellationToken cancellationToken);
}
