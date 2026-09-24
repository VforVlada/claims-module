namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Abstracts Hangfire's static BackgroundJob API so Application stays framework-agnostic;
/// implemented in Infrastructure.
/// </summary>
public interface IBackgroundJobScheduler
{
    void EnqueuePostGlReserveChange(Guid reserveHistoryId, Guid claimId, Guid reserveComponentId);
}
