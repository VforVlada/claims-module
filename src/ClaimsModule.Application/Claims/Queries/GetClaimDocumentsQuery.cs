using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Claims.Queries;

public sealed record GetClaimDocumentsQuery(Guid ClaimId) : IRequest<IReadOnlyCollection<ClaimDocumentDto>>, IQuery;
