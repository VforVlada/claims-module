using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands;

public sealed record OpenReserveCommand(
    Guid ClaimId,
    ReserveComponentType ComponentType,
    decimal Amount,
    string Currency = "USD",
    bool ManagerOverrideConfirmed = false,
    string? Reason = null) : IRequest<Result<ReserveComponentDto>>, ICommand;
