namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// The only sanctioned writer of ClaimAuditLog rows — enforced by convention (no handler
/// or job should touch IApplicationDbContext.ClaimAuditLogs directly).
/// </summary>
public interface IAuditLogService
{
    /// <param name="idempotencyKey">
    /// When set, must be globally unique — a second call with the same key fails the save
    /// (constraint violation) instead of writing a duplicate row. Used by background jobs that
    /// may run the same logical operation more than once concurrently (e.g. PostGlReserveChangeJob).
    /// </param>
    Task LogAsync(Guid claimId, string action, string? oldValues, string? newValues, string performedBy, CancellationToken cancellationToken, string? idempotencyKey = null);
}
