using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Entities;

public sealed class ClaimParty : BaseEntity
{
    public Guid ClaimId { get; set; }

    public PartyType PartyType { get; set; }

    public PartyRole PartyRole { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }
}
