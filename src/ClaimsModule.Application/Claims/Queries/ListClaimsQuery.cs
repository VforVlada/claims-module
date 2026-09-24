using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries;

public sealed record ListClaimsQuery(
    IReadOnlyCollection<ClaimStatus>? Statuses,
    DateTimeOffset? FromDate,
    DateTimeOffset? ToDate,
    string? AssignedHandler,
    Guid? CauseOfLossCodeId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<PagedList<ClaimListItemDto>>, IQuery;
