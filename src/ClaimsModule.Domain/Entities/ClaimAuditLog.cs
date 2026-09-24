using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Append-only. All writes must go through IAuditLogService — no direct DbContext
/// writes from any handler, and no UPDATE/DELETE access to this table anywhere.
/// </summary>
public sealed class ClaimAuditLog : BaseEntity
{
    public Guid ClaimId { get; set; }

    /// <summary>e.g. "CLAIM_CREATED", "STATUS_CHANGED", "RESERVE_APPROVED".</summary>
    public string Action { get; set; } = string.Empty;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string PerformedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional caller-supplied dedup key (e.g. ReserveHistory.IdempotencyKey for GL postings).
    /// Unique when set — this is the guard a "check, then insert" background job relies on
    /// under concurrent execution (see PostGlReserveChangeJob); most audit entries leave it null.
    /// </summary>
    public string? IdempotencyKey { get; set; }
}
