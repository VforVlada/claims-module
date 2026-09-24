using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Entities;

/// <summary>
/// Simulated, seeded, read-only from this module's perspective (no external policy
/// admin system integration in this assessment's scope).
/// </summary>
public sealed class Policy : BaseEntity
{
    public string PolicyNumber { get; set; } = string.Empty;

    public string ClientName { get; set; } = string.Empty;

    public DateTimeOffset EffectiveDate { get; set; }

    public DateTimeOffset ExpirationDate { get; set; }

    public ICollection<PolicyCoverage> Coverages { get; set; } = new List<PolicyCoverage>();

    public bool IsInForceOn(DateTimeOffset date) => date >= EffectiveDate && date <= ExpirationDate;
}
