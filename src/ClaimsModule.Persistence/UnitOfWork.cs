using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ClaimsModule.Persistence;

public sealed class UnitOfWork(ClaimsDbContext context) : IUnitOfWork, IAsyncDisposable, IDisposable
{
    private IDbContextTransaction? _transaction;

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        _transaction = await context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
        }
    }

    // Scopes disposed synchronously (e.g. a plain `using` scope outside ASP.NET) need this too.
    public void Dispose() => _transaction?.Dispose();
}
