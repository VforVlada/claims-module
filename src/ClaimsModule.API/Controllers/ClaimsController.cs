using ClaimsModule.API.Auth;
using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.Queries;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Route("api/claims")]
[Authorize(Roles = RoleNames.AnyRole)]
public sealed class ClaimsController(ISender mediator) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Result<ClaimDetailDto>>> Create(CreateClaimCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedList<ClaimListItemDto>>> List(
        [FromQuery] IReadOnlyCollection<ClaimStatus>? statuses,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] string? assignedHandler,
        [FromQuery] Guid? causeOfLossCodeId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListClaimsQuery(statuses, fromDate, toDate, assignedHandler, causeOfLossCodeId, pageNumber, pageSize);
        return Ok(await mediator.Send(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClaimDetailDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetClaimDetailQuery(id), cancellationToken));

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ClaimDetailDto>> TransitionStatus(Guid id, [FromBody] TransitionStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new TransitionClaimStatusCommand(id, request.NewStatus), cancellationToken));

    [HttpPost("{id:guid}/parties")]
    public async Task<ActionResult<ClaimPartyDto>> AddParty(Guid id, [FromBody] AddPartyRequest request, CancellationToken cancellationToken)
    {
        var command = new AddClaimPartyCommand(id, request.PartyType, request.PartyRole, request.Name, request.ContactEmail, request.ContactPhone);
        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<PagedList<ClaimAuditLogDto>>> GetAuditLog(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new GetClaimAuditLogQuery(id, pageNumber, pageSize), cancellationToken));

    public sealed record TransitionStatusRequest(ClaimStatus NewStatus);

    public sealed record AddPartyRequest(PartyType PartyType, PartyRole PartyRole, string Name, string? ContactEmail, string? ContactPhone);
}
