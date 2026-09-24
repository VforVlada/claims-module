using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reserves.Dtos;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands;

/// <summary>Sets a reserve component to a new amount (not a delta), with the reason for the change.</summary>
public sealed record AdjustReserveCommand(
    Guid ClaimId,
    Guid ReserveComponentId,
    decimal Amount,
    string Reason,
    string Currency = "USD",
    bool ManagerOverrideConfirmed = false) : IRequest<Result<ReserveComponentDto>>, ICommand;
