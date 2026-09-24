using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Policies.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Policies.Queries;

public sealed class SearchPoliciesQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<SearchPoliciesQuery, IReadOnlyCollection<PolicySearchResultDto>>
{
    public async Task<IReadOnlyCollection<PolicySearchResultDto>> Handle(SearchPoliciesQuery request, CancellationToken cancellationToken)
    {
        var term = request.SearchTerm.Trim();

        return await context.Policies.AsNoTracking()
            .Where(p => EF.Functions.Like(p.PolicyNumber, $"%{term}%") || EF.Functions.Like(p.ClientName, $"%{term}%"))
            .OrderBy(p => p.PolicyNumber)
            .Take(50)
            .ProjectTo<PolicySearchResultDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
