using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Services;

/// <summary>The only writer of ClaimAuditLog rows. Appends only — never updates or deletes.</summary>
public sealed class AuditLogService(ClaimsDbContext context, IDateTimeProvider dateTimeProvider) : IAuditLogService
{
    public async Task LogAsync(Guid claimId, string action, string? oldValues, string? newValues, string performedBy, CancellationToken cancellationToken, string? idempotencyKey = null)
    {
        // The audit row inherits the claim's tenant. Background jobs have no current user, so the
        // tenant query filter would hide the claim there and stamp Guid.Empty — making the row
        // invisible to the claim's own organization. Look it up past the filters instead.
        var organizationId = context.Claims.Local.FirstOrDefault(c => c.Id == claimId)?.OrganizationEntityId
            ?? await context.Claims
                .IgnoreQueryFilters()
                .Where(c => c.Id == claimId)
                .Select(c => (Guid?)c.OrganizationEntityId)
                .FirstOrDefaultAsync(cancellationToken);

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            OrganizationEntityId = organizationId ?? Guid.Empty,
            ClaimId = claimId,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            PerformedBy = performedBy,
            CreatedAt = dateTimeProvider.UtcNow,
            UserCreated = performedBy,
            IdempotencyKey = idempotencyKey
        });

        // Deliberately not wrapped: any other changes already tracked on this context (e.g. a
        // background job's ReserveHistory.MarkPosted) are flushed in this same save, so an
        // idempotency-key collision below rolls both back together — see PostGlReserveChangeJob.
        await context.SaveChangesAsync(cancellationToken);
    }
}
