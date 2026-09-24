using System.Security.Claims;
using ClaimsModule.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace ClaimsModule.Infrastructure.Tests.Auth;

public class CurrentUserServiceTests
{
    private static CurrentUserService CreateSut(ClaimsPrincipal? user)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = user is null ? null : new DefaultHttpContext { User = user }
        };
        return new CurrentUserService(accessor);
    }

    private static ClaimsPrincipal CreateUser(string userId, string userName, string orgId, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new("org_id", orgId)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public void Properties_WithAuthenticatedUser_ReadFromClaims()
    {
        var orgId = Guid.NewGuid();
        var sut = CreateSut(CreateUser("user-1", "Hannah Handler", orgId.ToString(), "Handler", "Supervisor"));

        Assert.Equal("user-1", sut.UserId);
        Assert.Equal("Hannah Handler", sut.UserName);
        Assert.Equal(orgId, sut.OrganizationEntityId);
        Assert.Equal(["Handler", "Supervisor"], sut.Roles);
        Assert.True(sut.IsInRole("Supervisor"));
        Assert.False(sut.IsInRole("Manager"));
    }

    [Fact]
    public void Properties_NoHttpContext_ReturnDefaults()
    {
        var sut = CreateSut(null);

        Assert.Equal("anonymous", sut.UserId);
        Assert.Equal("anonymous", sut.UserName);
        Assert.Equal(Guid.Empty, sut.OrganizationEntityId);
        Assert.Empty(sut.Roles);
        Assert.False(sut.IsInRole("Handler"));
    }

    [Fact]
    public void OrganizationEntityId_MissingOrInvalidClaim_ReturnsGuidEmpty()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        var sut = CreateSut(user);

        Assert.Equal(Guid.Empty, sut.OrganizationEntityId);
    }
}
