using System.Security.Claims;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace ClaimsModule.Infrastructure.Auth;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

    public string UserName => User?.FindFirstValue(ClaimTypes.Name) ?? "anonymous";

    public Guid OrganizationEntityId
    {
        get
        {
            var value = User?.FindFirstValue("org_id");
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    public IReadOnlyCollection<string> Roles => User?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
