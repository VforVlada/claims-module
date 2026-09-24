using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Application.Common.Services;

public sealed class ClaimStatusTransitionValidator(IApplicationDbContext context, IOptions<WorkflowSettings> workflowOptions) : IClaimStatusTransitionValidator
{
    public async Task<TransitionCheck> CheckAsync(ClaimStatus from, ClaimStatus to, IReadOnlyCollection<string> userRoles, CancellationToken cancellationToken)
    {
        var candidates = (await context.ClaimStatusTransitions
            .AsNoTracking()
            .Where(t => t.FromStatus == from && t.ToStatus == to)
            .ToListAsync(cancellationToken))
            .Where(IsEnabled)
            .ToList();

        if (candidates.Count == 0)
        {
            return TransitionCheck.NotInWorkflow;
        }

        return candidates.Any(t => userRoles.Contains(t.RequiredPermission, StringComparer.OrdinalIgnoreCase))
            ? TransitionCheck.Allowed
            : TransitionCheck.RoleNotPermitted;
    }

    public async Task<bool> IsAllowedAsync(ClaimStatus from, ClaimStatus to, IReadOnlyCollection<string> userRoles, CancellationToken cancellationToken) =>
        await CheckAsync(from, to, userRoles, cancellationToken) == TransitionCheck.Allowed;

    public async Task<IReadOnlyCollection<ClaimStatus>> GetAllowedNextStatusesAsync(ClaimStatus from, IReadOnlyCollection<string> userRoles, CancellationToken cancellationToken)
    {
        var transitions = await context.ClaimStatusTransitions
            .AsNoTracking()
            .Where(t => t.FromStatus == from)
            .ToListAsync(cancellationToken);

        return transitions
            .Where(IsEnabled)
            .Where(t => userRoles.Contains(t.RequiredPermission, StringComparer.OrdinalIgnoreCase))
            .Select(t => t.ToStatus)
            .Distinct()
            .ToList();
    }

    private bool IsEnabled(ClaimStatusTransition transition) =>
        workflowOptions.Value.AllowTrivialClaimClosure
        || transition is not { FromStatus: ClaimStatus.Open, ToStatus: ClaimStatus.Closed };
}
