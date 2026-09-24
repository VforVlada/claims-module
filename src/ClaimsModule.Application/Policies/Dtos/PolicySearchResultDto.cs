namespace ClaimsModule.Application.Policies.Dtos;

public sealed class PolicySearchResultDto
{
    public Guid PolicyId { get; init; }

    public string PolicyNumber { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public DateTimeOffset EffectiveDate { get; init; }

    public DateTimeOffset ExpirationDate { get; init; }

    /// <summary>Status today (the server's IDateTimeProvider clock), from the effective period.</summary>
    public PolicyStatus Status { get; init; }
}
