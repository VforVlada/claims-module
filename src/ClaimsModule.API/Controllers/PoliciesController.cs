using ClaimsModule.API.Auth;
using ClaimsModule.Application.Policies.Dtos;
using ClaimsModule.Application.Policies.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Route("api/policies")]
[Authorize(Roles = RoleNames.AnyRole)]
public sealed class PoliciesController(ISender mediator) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyCollection<PolicySearchResultDto>>> Search([FromQuery] string searchTerm, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new SearchPoliciesQuery(searchTerm), cancellationToken));

    [HttpGet("{id:guid}/coverage")]
    public async Task<ActionResult<IReadOnlyCollection<PolicyCoverageDto>>> GetCoverage(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetPolicyCoverageQuery(id), cancellationToken));
}
