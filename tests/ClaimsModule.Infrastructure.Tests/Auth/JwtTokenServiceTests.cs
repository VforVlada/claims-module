using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ClaimsModule.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Tests.Auth;

public class JwtTokenServiceTests
{
    private readonly JwtSettings _settings = new()
    {
        Issuer = "ClaimsModule",
        Audience = "ClaimsModule.Client",
        SigningKey = "unit-test-signing-key-at-least-32-bytes-long",
        ExpiryMinutes = 60
    };

    private JwtTokenService CreateSut() => new(Options.Create(_settings));

    [Fact]
    public void GenerateToken_IncludesExpectedClaims()
    {
        var sut = CreateSut();
        var organizationId = Guid.NewGuid();

        var token = sut.GenerateToken("user-1", "Hannah Handler", "Handler", organizationId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("user-1", jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("Hannah Handler", jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Handler", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(organizationId.ToString(), jwt.Claims.Single(c => c.Type == "org_id").Value);
    }

    [Fact]
    public void GenerateToken_SetsIssuerAndAudience()
    {
        var sut = CreateSut();

        var token = sut.GenerateToken("user-1", "Hannah Handler", "Handler", Guid.NewGuid());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal(_settings.Issuer, jwt.Issuer);
        Assert.Contains(_settings.Audience, jwt.Audiences);
    }

    [Fact]
    public void GenerateToken_ExpiresAfterConfiguredMinutes()
    {
        var sut = CreateSut();

        var token = sut.GenerateToken("user-1", "Hannah Handler", "Handler", Guid.NewGuid());

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expectedExpiry = DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes);
        Assert.True(Math.Abs((jwt.ValidTo - expectedExpiry).TotalMinutes) < 1);
    }
}
