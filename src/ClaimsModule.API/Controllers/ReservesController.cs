using ClaimsModule.API.Auth;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Reserves.Commands;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Application.Reserves.Queries;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Route("api/claims/{claimId:guid}/reserves")]
[Authorize(Roles = RoleNames.AnyRole)]
public sealed class ReservesController(ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Result<ReserveComponentDto>>> Open(Guid claimId, [FromBody] OpenReserveRequest request, CancellationToken cancellationToken)
    {
        var command = new OpenReserveCommand(claimId, request.ComponentType, request.Amount, request.Currency, request.ManagerOverrideConfirmed, request.Reason);
        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ReserveComponentDto>>> List(Guid claimId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListReservesQuery(claimId), cancellationToken));

    [HttpPut("{reserveId:guid}")]
    public async Task<ActionResult<Result<ReserveComponentDto>>> Adjust(Guid claimId, Guid reserveId, [FromBody] AdjustReserveRequest request, CancellationToken cancellationToken)
    {
        var command = new AdjustReserveCommand(claimId, reserveId, request.Amount, request.Reason, request.Currency, request.ManagerOverrideConfirmed);
        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpPost("{reserveId:guid}/approve")]
    [Authorize(Roles = RoleNames.SupervisorOrManager)]
    public async Task<ActionResult<ReserveComponentDto>> Approve(Guid claimId, Guid reserveId, [FromBody] ApproveReserveRequest request, CancellationToken cancellationToken)
    {
        var command = new ApproveReserveCommand(claimId, reserveId, request.ReserveHistoryId, request.ManagerOverrideConfirmed);
        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpPost("{reserveId:guid}/reject")]
    [Authorize(Roles = RoleNames.SupervisorOrManager)]
    public async Task<ActionResult<ReserveComponentDto>> Reject(Guid claimId, Guid reserveId, [FromBody] RejectReserveRequest request, CancellationToken cancellationToken)
    {
        var command = new RejectReserveCommand(claimId, reserveId, request.ReserveHistoryId, request.Reason);
        return Ok(await mediator.Send(command, cancellationToken));
    }

    public sealed record OpenReserveRequest(ReserveComponentType ComponentType, decimal Amount, string Currency = "USD", bool ManagerOverrideConfirmed = false, string? Reason = null);

    /// <summary>Amount is the reserve's new total, not a delta; Reason is required and kept on the history row.</summary>
    public sealed record AdjustReserveRequest(decimal Amount, string Reason, string Currency = "USD", bool ManagerOverrideConfirmed = false);

    public sealed record ApproveReserveRequest(Guid ReserveHistoryId, bool ManagerOverrideConfirmed = false);

    public sealed record RejectReserveRequest(Guid ReserveHistoryId, string Reason);
}
