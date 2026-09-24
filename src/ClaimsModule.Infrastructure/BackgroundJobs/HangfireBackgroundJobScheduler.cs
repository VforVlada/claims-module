using ClaimsModule.Application.Common.Interfaces;
using Hangfire;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Enqueues only once the command's transaction commits: domain events are dispatched inside
/// that transaction, and a job enqueued earlier could run before the rows it reads are visible,
/// or survive a rollback that removes them.
/// </summary>
public sealed class HangfireBackgroundJobScheduler(IBackgroundJobClient backgroundJobClient, IUnitOfWork unitOfWork) : IBackgroundJobScheduler
{
    public void EnqueuePostGlReserveChange(Guid reserveHistoryId, Guid claimId, Guid reserveComponentId) =>
        unitOfWork.OnCommitted(() => backgroundJobClient.Enqueue<PostGlReserveChangeJob>(job =>
            job.ExecuteAsync(reserveHistoryId, claimId, reserveComponentId, null)));
}
