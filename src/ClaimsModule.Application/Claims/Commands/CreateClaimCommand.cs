using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands;

public sealed record ClaimPartyInput(PartyType PartyType, PartyRole PartyRole, string Name, string? ContactEmail, string? ContactPhone);

public sealed record ClaimRiskObjectInput(AssetType AssetType, string Description, string? Identifier);

public sealed record InitialReserveInput(ReserveComponentType ComponentType, decimal Amount, string Currency = "USD");

public sealed record CreateClaimCommand(
    Guid? PolicyId,
    ClaimType ClaimType,
    DateTimeOffset LossDate,
    string LossDescription,
    string LossLocation,
    Guid CauseOfLossCodeId,
    string AssignedHandler,
    IReadOnlyCollection<ClaimPartyInput> Parties,
    IReadOnlyCollection<ClaimRiskObjectInput> RiskObjects,
    InitialReserveInput? InitialReserve) : IRequest<Result<ClaimDetailDto>>, ICommand;
