using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.ReferenceData.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.ReferenceData.Queries;

public sealed class ListCauseOfLossCodesQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<ListCauseOfLossCodesQuery, IReadOnlyCollection<CauseOfLossCodeDto>>
{
    public async Task<IReadOnlyCollection<CauseOfLossCodeDto>> Handle(ListCauseOfLossCodesQuery request, CancellationToken cancellationToken)
    {
        var query = context.CauseOfLossCodes.AsNoTracking().Where(c => c.IsActive);

        if (!string.IsNullOrWhiteSpace(request.PerilCategory))
        {
            query = query.Where(c => c.PerilCategory == request.PerilCategory);
        }

        return await query
            .OrderBy(c => c.Code)
            .ProjectTo<CauseOfLossCodeDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
