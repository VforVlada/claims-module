using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Policies.Dtos;
using MediatR;

namespace ClaimsModule.Application.Policies.Queries;

public sealed record GetPolicyCoverageQuery(Guid PolicyId) : IRequest<IReadOnlyCollection<PolicyCoverageDto>>, IQuery;
