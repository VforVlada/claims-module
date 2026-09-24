using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Seeded, data-driven status graph (assessment brief DDL) — validated by
/// IClaimStatusTransitionValidator instead of a hardcoded C# switch.
/// </summary>
public sealed class ClaimStatusTransition : BaseEntity
{
    public ClaimStatus FromStatus { get; set; }

    public ClaimStatus ToStatus { get; set; }

    /// <summary>Role name required to perform this transition, e.g. "Handler", "Supervisor".</summary>
    public string RequiredPermission { get; set; } = string.Empty;
}
