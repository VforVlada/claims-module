using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Models;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries;

public sealed record GetClaimAuditLogQuery(Guid ClaimId, int PageNumber = 1, int PageSize = 20) : IRequest<PagedList<ClaimAuditLogDto>>, IQuery;
