using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.Dtos;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reserves.Queries;

public sealed class ListReservesQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<ListReservesQuery, IReadOnlyCollection<ReserveComponentDto>>
{
    public async Task<IReadOnlyCollection<ReserveComponentDto>> Handle(ListReservesQuery request, CancellationToken cancellationToken)
    {
        return await context.ClaimReserveComponents.AsNoTracking()
            .Where(rc => rc.ClaimId == request.ClaimId)
            .ProjectTo<ReserveComponentDto>(mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);
    }
}
