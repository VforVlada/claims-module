namespace ClaimsModule.Application.Common.Models;

/// <summary>Configuration section "Workflow".</summary>
public sealed class WorkflowSettings
{
    public const string SectionName = "Workflow";

    /// <summary>
    /// The brief's "trivial claims" shortcut: Open → Closed without investigation or payment.
    /// On by default; switching it off removes that transition for every role (WF-03) while
    /// leaving the rest of the seeded graph untouched.
    /// </summary>
    public bool AllowTrivialClaimClosure { get; set; } = true;
}
