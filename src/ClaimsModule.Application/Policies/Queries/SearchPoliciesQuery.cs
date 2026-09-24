using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Policies.Dtos;
using MediatR;

namespace ClaimsModule.Application.Policies.Queries;

public sealed record SearchPoliciesQuery(string SearchTerm) : IRequest<IReadOnlyCollection<PolicySearchResultDto>>, IQuery;
