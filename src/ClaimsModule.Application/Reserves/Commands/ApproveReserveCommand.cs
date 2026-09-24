using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Reserves.Dtos;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands;

public sealed record ApproveReserveCommand(Guid ClaimId, Guid ReserveComponentId, Guid ReserveHistoryId, bool ManagerOverrideConfirmed = false)
    : IRequest<ReserveComponentDto>, ICommand;
