using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Policies.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Policies.Queries;

public sealed class GetPolicyCoverageQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<GetPolicyCoverageQuery, IReadOnlyCollection<PolicyCoverageDto>>
{
    public async Task<IReadOnlyCollection<PolicyCoverageDto>> Handle(GetPolicyCoverageQuery request, CancellationToken cancellationToken)
    {
        var exists = await context.Policies.AsNoTracking().AnyAsync(p => p.Id == request.PolicyId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(Domain.Entities.Policy), request.PolicyId);
        }

        return await context.PolicyCoverages.AsNoTracking()
            .Where(c => c.PolicyId == request.PolicyId)
            .ProjectTo<PolicyCoverageDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
