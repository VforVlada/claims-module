using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.ReferenceData.Dtos;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.ReferenceData.Queries;

public sealed class ListClaimStatusesQueryHandler(IClaimStatusTransitionValidator transitionValidator, ICurrentUserService currentUser)
    : IRequestHandler<ListClaimStatusesQuery, IReadOnlyCollection<ClaimStatusDto>>
{
    public async Task<IReadOnlyCollection<ClaimStatusDto>> Handle(ListClaimStatusesQuery request, CancellationToken cancellationToken)
    {
        var result = new List<ClaimStatusDto>();

        foreach (var status in Enum.GetValues<ClaimStatus>())
        {
            var allowedNext = await transitionValidator.GetAllowedNextStatusesAsync(status, currentUser.Roles, cancellationToken);
            result.Add(new ClaimStatusDto { Status = status, AllowedNextStatuses = allowedNext });
        }

        return result;
    }
}
