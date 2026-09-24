using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using Hangfire;
using Hangfire.Server;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Simulates posting a reserve change to the GL. Idempotency key is
/// ReserveHistory.IdempotencyKey ("Reserve:{ComponentId}:Change:{ChangeSequence}");
/// PostingStatus is checked before any write, but that check-then-act read is not itself
/// the guard against a concurrent duplicate post (two executions can both pass it before
/// either commits) — the real guard is the unique index on ClaimAuditLog.IdempotencyKey,
/// see the catch block below.
/// </summary>
public sealed class PostGlReserveChangeJob(IApplicationDbContext context, IAuditLogService auditLog)
{
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task ExecuteAsync(Guid reserveHistoryId, Guid claimId, Guid reserveComponentId, PerformContext? hangfireContext = null)
    {
        // No HTTP user in a Hangfire job, so the tenant query filter would hide every row (it
        // resolves to Guid.Empty) — the reserveHistoryId was issued by an already-authorized request.
        var history = await context.ReserveHistories
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(h => h.Id == reserveHistoryId && !h.IsDeleted);
        if (history is null || history.PostingStatus == PostingStatus.Posted)
        {
            return;
        }

        var jobId = hangfireContext?.BackgroundJob.Id ?? "local";

        try
        {
            history.MarkPosted(jobId);

            // Not saved here on purpose: LogAsync's own SaveChangesAsync below flushes this
            // change together with the new audit row in one transaction, so a unique-index
            // collision on IdempotencyKey rolls both back together (see the catch below).
            var journalDescription = JournalEntry(history.Amount, reserveComponentId, history.IdempotencyKey);
            await auditLog.LogAsync(claimId, "GL_POSTING_SIMULATED", null, journalDescription, "system", CancellationToken.None, history.IdempotencyKey);
        }
        catch (DbUpdateException)
        {
            // Another execution of this same reserveHistoryId (a Hangfire retry racing the
            // original, or a duplicate enqueue) already won and posted it first. This
            // execution's audit-row insert collided with that one's IdempotencyKey, so
            // nothing from this run persisted — exit cleanly (I-JOB-02, I-JOB-03).
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            history.MarkPostingFailed(jobId);
            await context.SaveChangesAsync(CancellationToken.None);
            await auditLog.LogAsync(claimId, "GL_POSTING_FAILED", null, ex.Message, "system", CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// The simulated journal for a change in outstanding reserves. An increase debits the
    /// expense account (Change in Outstanding Reserves) and credits the liability (Outstanding
    /// Loss Reserves); a decrease reverses the two lines.
    /// </summary>
    public static string JournalEntry(Money change, Guid reserveComponentId, string idempotencyKey)
    {
        const string expense = "Change in Outstanding Reserves";
        const string liability = "Outstanding Loss Reserves";
        var (debit, credit) = change.Amount >= 0 ? (expense, liability) : (liability, expense);
        var amount = new Money(Math.Abs(change.Amount), change.Currency);
        return $"DR {debit} {amount} / CR {credit} {amount} — reserve component {reserveComponentId} ({idempotencyKey})";
    }
}
