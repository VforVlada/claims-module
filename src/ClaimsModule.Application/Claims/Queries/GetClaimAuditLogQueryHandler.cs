using AutoMapper;
using AutoMapper.QueryableExtensions;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries;

public sealed class GetClaimAuditLogQueryHandler(IApplicationDbContext context, IMapper mapper)
    : IRequestHandler<GetClaimAuditLogQuery, PagedList<ClaimAuditLogDto>>
{
    public Task<PagedList<ClaimAuditLogDto>> Handle(GetClaimAuditLogQuery request, CancellationToken cancellationToken)
    {
        var query = context.ClaimAuditLogs.AsNoTracking()
            .Where(a => a.ClaimId == request.ClaimId)
            .OrderByDescending(a => a.CreatedAt)
            .ProjectTo<ClaimAuditLogDto>(mapper.ConfigurationProvider);

        return PagedList<ClaimAuditLogDto>.CreateAsync(query, request.PageNumber, request.PageSize, cancellationToken);
    }
}
