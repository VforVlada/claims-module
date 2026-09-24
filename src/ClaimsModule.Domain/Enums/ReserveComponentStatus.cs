namespace ClaimsModule.Domain.Enums;

/// <summary>
/// Whether a reserve line is still active. It follows the claim's lifecycle: every line closes
/// when the claim is Closed or Withdrawn, and reopens when the claim is Reopened. A closed line
/// accepts no new changes or approvals.
/// </summary>
public enum ReserveComponentStatus
{
    Open = 0,
    Closed = 1
}
