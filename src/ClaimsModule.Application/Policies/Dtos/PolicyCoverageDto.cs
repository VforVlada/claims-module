namespace ClaimsModule.Application.Policies.Dtos;

public sealed class PolicyCoverageDto
{
    public Guid Id { get; init; }

    public string CoverageType { get; init; } = string.Empty;

    public decimal Limit { get; init; }

    public decimal Deductible { get; init; }

    public string Currency { get; init; } = "USD";
}
