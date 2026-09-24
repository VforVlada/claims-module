namespace ClaimsModule.Application.Policies.Dtos;

public sealed class PolicySearchResultDto
{
    public Guid Id { get; init; }

    public string PolicyNumber { get; init; } = string.Empty;

    public string ClientName { get; init; } = string.Empty;

    public DateTimeOffset EffectiveDate { get; init; }

    public DateTimeOffset ExpirationDate { get; init; }

    public bool IsInForce { get; init; }
}
