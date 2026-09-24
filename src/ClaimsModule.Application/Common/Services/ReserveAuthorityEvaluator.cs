using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Common.Services;

public sealed class ReserveAuthorityEvaluator : IReserveAuthorityEvaluator
{
    private static readonly decimal SupervisorThreshold = 10_000m;
    private static readonly decimal ManagerThreshold = 100_000m;
    private static readonly decimal AggregateCap = 10_000_000m;

    public ApprovalTier EvaluateTier(Money amount)
    {
        var absoluteAmount = Math.Abs(amount.Amount);

        return absoluteAmount switch
        {
            _ when absoluteAmount <= SupervisorThreshold => ApprovalTier.Auto,
            _ when absoluteAmount <= ManagerThreshold => ApprovalTier.Supervisor,
            _ => ApprovalTier.Manager
        };
    }

    public bool ExceedsAggregateCap(Money aggregateTotal) => Math.Abs(aggregateTotal.Amount) > AggregateCap;

    public bool CanApprove(ApprovalTier tier, IReadOnlyCollection<string> approverRoles) => tier switch
    {
        ApprovalTier.Supervisor => HasRole(approverRoles, RoleNames.Supervisor) || HasRole(approverRoles, RoleNames.Manager),
        ApprovalTier.Manager => HasRole(approverRoles, RoleNames.Manager),
        _ => false
    };

    private static bool HasRole(IReadOnlyCollection<string> roles, string role) =>
        roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
