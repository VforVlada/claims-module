using AutoMapper;
using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reserves.Commands;

public sealed class AdjustReserveCommandHandler(
    IApplicationDbContext context,
    IReserveAuthorityEvaluator authorityEvaluator,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<AdjustReserveCommand, Result<ReserveComponentDto>>
{
    public async Task<Result<ReserveComponentDto>> Handle(AdjustReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims
            .Include(c => c.ReserveComponents).ThenInclude(rc => rc.History)
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);

        var component = claim.ReserveComponents.SingleOrDefault(rc => rc.Id == request.ReserveComponentId)
            ?? throw new NotFoundException(nameof(Domain.Entities.ClaimReserveComponent), request.ReserveComponentId);

        var amount = new Money(request.Amount, request.Currency);
        var tier = authorityEvaluator.EvaluateTier(amount);
        component.SubmitChange(amount, tier, currentUser.UserName, request.Reason);

        var warnings = new List<ValidationIssue>();
        var aggregateTotal = claim.ReserveComponents.Aggregate(Money.Zero(amount.Currency), (sum, rc) => sum + rc.CurrentAmount);
        if (authorityEvaluator.ExceedsAggregateCap(aggregateTotal))
        {
            if (!request.ManagerOverrideConfirmed)
            {
                throw new AggregateReserveCapExceededException("Aggregate reserve exceeds $10,000,000; a manager override is required to proceed.");
            }

            // Same rule as ApproveReserveCommandHandler: the override flag is only honoured from a Manager,
            // otherwise any role could bypass BR-R-07 by sending it straight to the API.
            if (!currentUser.IsInRole(RoleNames.Manager))
            {
                throw new ForbiddenAccessException("Only a Manager can override the $10,000,000 aggregate reserve cap.");
            }

            claim.FlagManagerOverrideRequired();
            warnings.Add(new ValidationIssue("BR-R-07", "Aggregate reserve exceeds $10,000,000 and requires manager override.", nameof(request.Amount)));
        }

        await context.SaveChangesAsync(cancellationToken);

        return Result<ReserveComponentDto>.Success(mapper.Map<ReserveComponentDto>(component), warnings);
    }
}
