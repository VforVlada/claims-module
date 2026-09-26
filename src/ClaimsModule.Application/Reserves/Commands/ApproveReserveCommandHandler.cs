using AutoMapper;
using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reserves.Commands;

public sealed class ApproveReserveCommandHandler(
    IApplicationDbContext context,
    IReserveAuthorityEvaluator authorityEvaluator,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IMapper mapper) : IRequestHandler<ApproveReserveCommand, ReserveComponentDto>
{
    public async Task<ReserveComponentDto> Handle(ApproveReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims
            .Include(c => c.ReserveComponents).ThenInclude(rc => rc.History)
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);

        var component = claim.ReserveComponents.SingleOrDefault(rc => rc.Id == request.ReserveComponentId)
            ?? throw new NotFoundException(nameof(Domain.Entities.ClaimReserveComponent), request.ReserveComponentId);

        // Only meaningful while the change is still pending — once it's already Approved/Rejected,
        // component.Approve() below raises the correct "invalid state" error on its own (RES-06).
        var history = component.History.SingleOrDefault(h => h.Id == request.ReserveHistoryId);
        if (history is not null && history.ApprovalStatus == ApprovalStatus.PendingApproval)
        {
            if (!authorityEvaluator.CanApprove(history.RequiredTier, currentUser.Roles))
            {
                throw new ForbiddenAccessException($"The current user's role does not have authority to approve a {history.RequiredTier}-tier reserve change.");
            }

            if (string.Equals(history.RequestedBy, currentUser.UserName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ForbiddenAccessException("A reserve change cannot be approved by the user who requested it.");
            }
        }

        component.Approve(request.ReserveHistoryId, currentUser.UserName, dateTimeProvider.UtcNow);

        // BR-R-07: a pending change only joins the balance when approved, so this is the first
        // point a large reserve can push the claim over the aggregate cap. Throwing here rolls the
        // approval back with the rest of the unit of work.
        var aggregateTotal = claim.ReserveComponents.Aggregate(Money.Zero(component.CurrentAmount.Currency), (sum, rc) => sum + rc.CurrentAmount);
        var exceedsCap = authorityEvaluator.ExceedsAggregateCap(aggregateTotal);
        if (exceedsCap)
        {
            if (!request.ManagerOverrideConfirmed)
            {
                throw new AggregateReserveCapExceededException("Approving this change takes the claim's aggregate reserve over $10,000,000; a manager override is required.");
            }

            if (!currentUser.IsInRole(RoleNames.Manager))
            {
                throw new ForbiddenAccessException("Only a Manager can override the $10,000,000 aggregate reserve cap.");
            }

            claim.RaiseWarning("BR-R-07", "Aggregate reserve exceeds $10,000,000; approved under manager override.", currentUser.UserName);
        }

        claim.UpdateAggregateCapFlag(exceedsCap);

        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<ReserveComponentDto>(component);
    }
}
