namespace ClaimsModule.Application.ReferenceData.Dtos;

public sealed class CauseOfLossCodeDto
{
    public Guid Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string PerilCategory { get; init; } = string.Empty;
}
