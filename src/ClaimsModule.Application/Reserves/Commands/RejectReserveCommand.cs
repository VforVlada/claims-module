using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Application.Reserves.Dtos;
using MediatR;

namespace ClaimsModule.Application.Reserves.Commands;

public sealed record RejectReserveCommand(Guid ClaimId, Guid ReserveComponentId, Guid ReserveHistoryId, string Reason)
    : IRequest<ReserveComponentDto>, ICommand;
