using ApplicationRoleNames = ClaimsModule.Application.Common.Constants.RoleNames;

namespace ClaimsModule.API.Auth;

public static class RoleNames
{
    public const string Handler = ApplicationRoleNames.Handler;
    public const string Supervisor = ApplicationRoleNames.Supervisor;
    public const string Manager = ApplicationRoleNames.Manager;

    public const string SupervisorOrManager = $"{Supervisor},{Manager}";
    public const string AnyRole = $"{Handler},{Supervisor},{Manager}";
}
