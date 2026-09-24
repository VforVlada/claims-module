namespace ClaimsModule.Application.Common.Models;

public enum TransitionCheck
{
    Allowed,

    /// <summary>The (from, to) pair isn't part of the workflow for anyone — maps to 409.</summary>
    NotInWorkflow,

    /// <summary>The pair exists, but none of the caller's roles carries its permission — maps to 403.</summary>
    RoleNotPermitted
}
