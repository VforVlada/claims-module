using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ClaimsModule.Application.Claims;

namespace ClaimsModule.Application.Claims.Commands;

public sealed class TransitionClaimStatusCommandHandler(
    IApplicationDbContext context,
    IClaimStatusTransitionValidator transitionValidator,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<TransitionClaimStatusCommand, ClaimDetailDto>
{
    public async Task<ClaimDetailDto> Handle(TransitionClaimStatusCommand request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims.IncludeFullGraph()
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);

        // Two different failures (WF-02 vs WF-05): a pair that isn't in the workflow at all is an
        // illegal state change (409, with the legal alternatives listed); a real transition the
        // caller's role doesn't carry the permission for is an authorization failure (403).
        switch (await transitionValidator.CheckAsync(claim.Status, request.NewStatus, currentUser.Roles, cancellationToken))
        {
            case TransitionCheck.RoleNotPermitted:
                throw new ForbiddenAccessException($"Your role is not permitted to move a claim from {claim.Status} to {request.NewStatus}.");
            case TransitionCheck.NotInWorkflow:
                var allowedNext = await transitionValidator.GetAllowedNextStatusesAsync(claim.Status, currentUser.Roles, cancellationToken);
                throw new InvalidClaimStatusTransitionException(claim.Status, request.NewStatus, allowedNext);
        }

        claim.TransitionTo(request.NewStatus, currentUser.UserName);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<ClaimDetailDto>(claim);
    }
}
