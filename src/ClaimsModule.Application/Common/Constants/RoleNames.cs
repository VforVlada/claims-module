namespace ClaimsModule.Application.Common.Constants;

/// <summary>Canonical role names, shared by API authorization attributes and Application-layer authority checks (e.g. IReserveAuthorityEvaluator.CanApprove) so the two never drift apart.</summary>
public static class RoleNames
{
    public const string Handler = "Handler";
    public const string Supervisor = "Supervisor";
    public const string Manager = "Manager";
}
