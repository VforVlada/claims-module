namespace ClaimsModule.Application.Claims.Dtos;

public sealed class LossEventDto
{
    public DateTimeOffset LossDate { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public Guid CauseOfLossCodeId { get; init; }

    public string? CauseOfLossCode { get; init; }

    public string? CauseOfLossDescription { get; init; }
}
