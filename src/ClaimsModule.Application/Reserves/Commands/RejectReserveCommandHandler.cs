using AutoMapper;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reserves.Commands;

public sealed class RejectReserveCommandHandler(
    IApplicationDbContext context,
    IReserveAuthorityEvaluator authorityEvaluator,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IMapper mapper) : IRequestHandler<RejectReserveCommand, ReserveComponentDto>
{
    public async Task<ReserveComponentDto> Handle(RejectReserveCommand request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims
            .Include(c => c.ReserveComponents).ThenInclude(rc => rc.History)
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);

        var component = claim.ReserveComponents.SingleOrDefault(rc => rc.Id == request.ReserveComponentId)
            ?? throw new NotFoundException(nameof(Domain.Entities.ClaimReserveComponent), request.ReserveComponentId);

        // Same authority as approving (RES-05): a supervisor can neither approve nor reject a
        // Manager-tier change. Already-decided changes fall through to the domain's "invalid state".
        var history = component.History.SingleOrDefault(h => h.Id == request.ReserveHistoryId);
        if (history is { ApprovalStatus: ApprovalStatus.PendingApproval } && !authorityEvaluator.CanApprove(history.RequiredTier, currentUser.Roles))
        {
            throw new ForbiddenAccessException($"The current user's role does not have authority to reject a {history.RequiredTier}-tier reserve change.");
        }

        component.Reject(request.ReserveHistoryId, currentUser.UserName, request.Reason, dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<ReserveComponentDto>(component);
    }
}
