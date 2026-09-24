using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Common.Interfaces;

/// <summary>
/// Allocates a claim number atomically (single DB round-trip via a SEQUENCE or
/// UPDATE...OUTPUT INSERTED counter table) — never an application-level read-then-increment.
/// </summary>
public interface IClaimNumberGenerator
{
    Task<ClaimNumber> NextAsync(CancellationToken cancellationToken);
}
