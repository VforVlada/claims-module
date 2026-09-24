using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// BR-R-02..04 authority thresholds: <=$10K auto, >$10K-$100K supervisor,
/// >$100K manager. BR-R-07: aggregate claim reserves >$10M require manager override.
/// </summary>
public interface IReserveAuthorityEvaluator
{
    ApprovalTier EvaluateTier(Money amount);

    bool ExceedsAggregateCap(Money aggregateTotal);

    /// <summary>BR-R-05: a Manager may approve any tier; a Supervisor may approve Supervisor-tier only; Auto-tier is never manually approved.</summary>
    bool CanApprove(ApprovalTier tier, IReadOnlyCollection<string> approverRoles);
}
