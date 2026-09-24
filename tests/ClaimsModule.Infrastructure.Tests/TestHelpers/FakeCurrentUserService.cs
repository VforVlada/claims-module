using ClaimsModule.Application.Common.Interfaces;

namespace ClaimsModule.Infrastructure.Tests.TestHelpers;

public sealed class FakeCurrentUserService : ICurrentUserService
{
    public string UserId { get; set; } = "user-1";

    public string UserName { get; set; } = "Test User";

    public Guid OrganizationEntityId { get; set; } = Guid.NewGuid();

    public IReadOnlyCollection<string> Roles { get; set; } = ["Handler"];

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
