namespace ClaimsModule.Application.Claims.Dtos;

public sealed class ClaimAuditLogDto
{
    public Guid Id { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public string PerformedBy { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
