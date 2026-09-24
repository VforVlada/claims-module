using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands;

public sealed record AddClaimPartyCommand(
    Guid ClaimId,
    PartyType PartyType,
    PartyRole PartyRole,
    string Name,
    string? ContactEmail,
    string? ContactPhone) : IRequest<ClaimPartyDto>, ICommand;
