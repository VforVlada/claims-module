namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Transaction demarcation only. Handlers persist through IApplicationDbContext.SaveChangesAsync
/// directly (so they get server-generated values back before building their response); this
/// interface just gives UnitOfWorkBehavior an ambient transaction so that call and any other
/// round trip within the same command (e.g. IClaimNumberGenerator) are atomic together.
/// </summary>
public interface IUnitOfWork
{
    Task BeginTransactionAsync(CancellationToken cancellationToken);

    Task CommitTransactionAsync(CancellationToken cancellationToken);

    Task RollbackTransactionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Defers a side effect outside the database (e.g. enqueueing a Hangfire job) until the open
    /// transaction commits, and drops it on rollback. Runs it immediately when no transaction is open.
    /// </summary>
    void OnCommitted(Action action);
}
