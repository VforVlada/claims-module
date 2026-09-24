using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.Dtos;

public sealed class ClaimPartyDto
{
    public Guid Id { get; init; }

    public PartyType PartyType { get; init; }

    public PartyRole PartyRole { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }
}
