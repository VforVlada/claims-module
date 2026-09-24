using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Recurring (every 15 min). Flags claims stuck in Draft/Open for 48h+ as SLA-breached
/// (Claim.IsSlaBreached) and writes one SLA_BREACH_DETECTED audit entry per breach. The flag is
/// the dedup: an already-flagged claim is skipped, and any activity on the claim clears the flag,
/// so a claim that goes stale again later is flagged (and audited) again.
/// </summary>
public sealed class SlaMonitoringJob(IApplicationDbContext context, IAuditLogService auditLog, IDateTimeProvider dateTimeProvider)
{
    private static readonly TimeSpan SlaWindow = TimeSpan.FromHours(48);

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var staleBefore = now - SlaWindow;

        // Runs outside any HTTP request, so there is no current user and the tenant query
        // filter would resolve to Guid.Empty and hide every row. This job is cross-tenant by
        // design: bypass the filters and re-apply the soft-delete condition by hand.
        var staleClaims = await context.Claims
            .IgnoreQueryFilters()
            .Where(c => !c.IsDeleted
                && !c.IsSlaBreached
                && (c.Status == ClaimStatus.Draft || c.Status == ClaimStatus.Open)
                && (c.UpdatedAt ?? c.CreatedAt) < staleBefore)
            .ToListAsync(cancellationToken);

        foreach (var claim in staleClaims)
        {
            var lastActivity = claim.UpdatedAt ?? claim.CreatedAt;
            if (claim.FlagSlaBreach(now))
            {
                // LogAsync saves, so the flag and its audit entry commit together.
                await auditLog.LogAsync(claim.Id, "SLA_BREACH_DETECTED", null, $"Status={claim.Status}; LastActivity={lastActivity:O}", "system", cancellationToken);
            }
        }
    }
}
