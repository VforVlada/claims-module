using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands;

public sealed record TransitionClaimStatusCommand(Guid ClaimId, ClaimStatus NewStatus) : IRequest<ClaimDetailDto>, ICommand;
