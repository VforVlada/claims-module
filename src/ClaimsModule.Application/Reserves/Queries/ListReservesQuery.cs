using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Reserves.Dtos;
using MediatR;

namespace ClaimsModule.Application.Reserves.Queries;

public sealed record ListReservesQuery(Guid ClaimId) : IRequest<IReadOnlyCollection<ReserveComponentDto>>, IQuery;
