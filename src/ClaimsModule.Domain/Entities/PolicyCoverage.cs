using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Entities;

public sealed class PolicyCoverage : BaseEntity
{
    public Guid PolicyId { get; set; }

    public Policy? Policy { get; set; }

    public string CoverageType { get; set; } = string.Empty;

    public Money Limit { get; set; }

    public Money Deductible { get; set; }
}
