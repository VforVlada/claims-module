namespace ClaimsModule.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string UserId { get; }

    string UserName { get; }

    Guid OrganizationEntityId { get; }

    IReadOnlyCollection<string> Roles { get; }

    bool IsInRole(string role);
}
