namespace ClaimsModule.Infrastructure.Auth;

public interface IJwtTokenService
{
    string GenerateToken(string userId, string userName, string role, Guid organizationEntityId);
}
