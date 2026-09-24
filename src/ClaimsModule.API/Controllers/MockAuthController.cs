using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsModule.API.Controllers;

/// <summary>
/// Mock login for the assessment's role-switcher — no real credential store. Issues a JWT
/// for one of the hardcoded users: handler/supervisor/manager in the default seeded org, plus
/// one handler in a second org so tenant isolation can be exercised end to end.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class MockAuthController(IJwtTokenService jwtTokenService) : ControllerBase
{
    private static readonly Guid DefaultOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondOrganizationId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Dictionary<string, (string UserId, string UserName, string Role, Guid OrganizationId)> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        ["handler"] = ("user-handler-1", "Hannah Handler", Auth.RoleNames.Handler, DefaultOrganizationId),
        ["supervisor"] = ("user-supervisor-1", "Sam Supervisor", Auth.RoleNames.Supervisor, DefaultOrganizationId),
        ["manager"] = ("user-manager-1", "Morgan Manager", Auth.RoleNames.Manager, DefaultOrganizationId),
        ["orgb-handler"] = ("user-orgb-handler-1", "Blake OrgB", Auth.RoleNames.Handler, SecondOrganizationId)
    };

    public sealed record MockLoginRequest(string Role);

    public sealed record MockLoginResponse(string Token, string UserId, string UserName, string Role);

    [HttpGet("users")]
    public ActionResult<IEnumerable<string>> ListUsers() => Ok(Users.Keys);

    [HttpPost("mock-login")]
    public ActionResult<MockLoginResponse> MockLogin([FromBody] MockLoginRequest request)
    {
        if (!Users.TryGetValue(request.Role, out var user))
        {
            return BadRequest(new { error = $"Unknown role '{request.Role}'. Valid values: {string.Join(", ", Users.Keys)}." });
        }

        var token = jwtTokenService.GenerateToken(user.UserId, user.UserName, user.Role, user.OrganizationId);
        return Ok(new MockLoginResponse(token, user.UserId, user.UserName, user.Role));
    }
}
