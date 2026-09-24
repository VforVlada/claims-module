using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Policies.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Policies.Queries;

public sealed class SearchPoliciesQueryHandler(IApplicationDbContext context, IMapper mapper, IDateTimeProvider dateTimeProvider)
    : IRequestHandler<SearchPoliciesQuery, IReadOnlyCollection<PolicySearchResultDto>>
{
    public async Task<IReadOnlyCollection<PolicySearchResultDto>> Handle(SearchPoliciesQuery request, CancellationToken cancellationToken)
    {
        var pattern = $"%{EscapeLikeWildcards(request.SearchTerm.Trim())}%";

        return await context.Policies.AsNoTracking()
            .Where(p => EF.Functions.Like(p.PolicyNumber, pattern) || EF.Functions.Like(p.ClientName, pattern))
            .OrderBy(p => p.PolicyNumber)
            .Take(50)
            .ProjectTo<PolicySearchResultDto>(mapper.ConfigurationProvider, new { today = dateTimeProvider.UtcNow })
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The term is already a SQL parameter, so this is about meaning, not injection: a typed "%", "_"
    /// or "[" should match itself rather than act as a LIKE wildcard.
    /// </summary>
    private static string EscapeLikeWildcards(string term) =>
        term.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
