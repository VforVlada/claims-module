using ClaimsModule.Application.Common.Interfaces;
using Hangfire;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

public sealed class HangfireBackgroundJobScheduler(IBackgroundJobClient backgroundJobClient) : IBackgroundJobScheduler
{
    public void EnqueuePostGlReserveChange(Guid reserveHistoryId, Guid claimId, Guid reserveComponentId) =>
        backgroundJobClient.Enqueue<PostGlReserveChangeJob>(job =>
            job.ExecuteAsync(reserveHistoryId, claimId, reserveComponentId, null));
}
