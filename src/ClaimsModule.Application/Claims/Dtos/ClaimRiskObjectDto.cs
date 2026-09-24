using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.Dtos;

public sealed class ClaimRiskObjectDto
{
    public Guid Id { get; init; }

    public AssetType AssetType { get; init; }

    public string Description { get; init; } = string.Empty;

    public string? Identifier { get; init; }
}
