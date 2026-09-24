using ClaimsModule.API.Auth;
using ClaimsModule.Application.ReferenceData.Dtos;
using ClaimsModule.Application.ReferenceData.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Route("api/reference")]
[Authorize(Roles = RoleNames.AnyRole)]
public sealed class ReferenceDataController(ISender mediator) : ControllerBase
{
    [HttpGet("cause-of-loss-codes")]
    public async Task<ActionResult<IReadOnlyCollection<CauseOfLossCodeDto>>> ListCauseOfLossCodes([FromQuery] string? perilCategory, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListCauseOfLossCodesQuery(perilCategory), cancellationToken));

    [HttpGet("claim-statuses")]
    public async Task<ActionResult<IReadOnlyCollection<ClaimStatusDto>>> ListClaimStatuses(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListClaimStatusesQuery(), cancellationToken));
}
