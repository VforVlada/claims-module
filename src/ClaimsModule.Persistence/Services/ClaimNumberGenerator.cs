using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence.Services;

/// <summary>
/// Allocates via a single "SELECT NEXT VALUE FOR" round trip against a SQL SEQUENCE
/// (see ClaimsDbContext.OnModelCreating) — no app-level read-then-increment (BR-C-04).
/// </summary>
public sealed class ClaimNumberGenerator(ClaimsDbContext context, IDateTimeProvider dateTimeProvider) : IClaimNumberGenerator
{
    public async Task<ClaimNumber> NextAsync(CancellationToken cancellationToken)
    {
        // Materialize with ToListAsync rather than FirstAsync: composing a Take(1)/TOP(1) onto
        // SqlQueryRaw wraps "NEXT VALUE FOR" in a derived table, which SQL Server rejects.
        var sequenceValues = await context.Database
            .SqlQueryRaw<long>("SELECT NEXT VALUE FOR dbo.ClaimNumberSequence AS Value")
            .ToListAsync(cancellationToken);

        return ClaimNumber.Create(dateTimeProvider.UtcNow.Year, sequenceValues[0]);
    }
}
