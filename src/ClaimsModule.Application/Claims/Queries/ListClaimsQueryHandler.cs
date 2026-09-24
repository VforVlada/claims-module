using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries;

public sealed class ListClaimsQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<ListClaimsQuery, PagedList<ClaimListItemDto>>
{
    public Task<PagedList<ClaimListItemDto>> Handle(ListClaimsQuery request, CancellationToken cancellationToken)
    {
        var query = context.Claims.AsNoTracking().AsQueryable();

        if (request.Statuses is { Count: > 0 })
        {
            query = query.Where(c => request.Statuses.Contains(c.Status));
        }

        if (request.FromDate is not null)
        {
            query = query.Where(c => c.LossEvent.LossDate >= request.FromDate);
        }

        if (request.ToDate is not null)
        {
            query = query.Where(c => c.LossEvent.LossDate <= request.ToDate);
        }

        if (!string.IsNullOrWhiteSpace(request.AssignedHandler))
        {
            query = query.Where(c => c.AssignedHandler.Contains(request.AssignedHandler));
        }

        if (request.CauseOfLossCodeId is not null)
        {
            query = query.Where(c => c.LossEvent.CauseOfLossCodeId == request.CauseOfLossCodeId);
        }

        var projected = query
            .OrderByDescending(c => c.CreatedAt)
            .ThenBy(c => c.Id) // stable tiebreak so paging never repeats or skips rows created in one batch
            .ProjectTo<ClaimListItemDto>(mapper.ConfigurationProvider);

        return PagedList<ClaimListItemDto>.CreateAsync(projected, request.PageNumber, request.PageSize, cancellationToken);
    }
}
