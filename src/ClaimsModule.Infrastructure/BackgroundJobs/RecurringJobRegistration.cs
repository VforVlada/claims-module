using Hangfire;

namespace ClaimsModule.Infrastructure.BackgroundJobs;

/// <summary>
/// Registers every recurring Hangfire job. Called once at startup; AddOrUpdate is idempotent,
/// so restarts (or several API instances) never duplicate a schedule.
/// </summary>
public static class RecurringJobRegistration
{
    public const string SlaMonitoringJobId = "sla-monitoring";

    /// <summary>Every 15 minutes (JOB-05).</summary>
    public const string SlaMonitoringCron = "*/15 * * * *";

    public static void RegisterRecurringJobs(this IRecurringJobManager recurringJobManager) =>
        recurringJobManager.AddOrUpdate<SlaMonitoringJob>(
            SlaMonitoringJobId,
            job => job.ExecuteAsync(CancellationToken.None),
            SlaMonitoringCron);
}
