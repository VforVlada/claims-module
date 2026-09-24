using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Mirrors ClaimsModule.Persistence.Services.AuditLogService's behavior against
/// IApplicationDbContext instead of the concrete (sealed) ClaimsDbContext, so background job
/// tests can exercise a real audit-writing dependency without needing that concrete type.
/// </summary>
public sealed class TestAuditLogService(IApplicationDbContext context, IDateTimeProvider dateTimeProvider) : IAuditLogService
{
    public async Task LogAsync(Guid claimId, string action, string? oldValues, string? newValues, string performedBy, CancellationToken cancellationToken, string? idempotencyKey = null)
    {
        var claim = await context.Claims.FindAsync([claimId], cancellationToken);

        context.ClaimAuditLogs.Add(new ClaimAuditLog
        {
            OrganizationEntityId = claim?.OrganizationEntityId ?? Guid.Empty,
            ClaimId = claimId,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            PerformedBy = performedBy,
            CreatedAt = dateTimeProvider.UtcNow,
            UserCreated = performedBy,
            IdempotencyKey = idempotencyKey
        });

        await context.SaveChangesAsync(cancellationToken);
    }
}
