using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries;

public sealed record GetClaimDetailQuery(Guid ClaimId) : IRequest<ClaimDetailDto>, IQuery;
