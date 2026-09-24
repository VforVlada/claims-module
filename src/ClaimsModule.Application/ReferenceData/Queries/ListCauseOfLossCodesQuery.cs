using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.ReferenceData.Dtos;
using MediatR;

namespace ClaimsModule.Application.ReferenceData.Queries;

public sealed record ListCauseOfLossCodesQuery(string? PerilCategory) : IRequest<IReadOnlyCollection<CauseOfLossCodeDto>>, IQuery;
