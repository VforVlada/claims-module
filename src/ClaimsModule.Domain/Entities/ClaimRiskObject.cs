using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Entities;

public sealed class ClaimRiskObject : BaseEntity
{
    public Guid ClaimId { get; set; }

    public AssetType AssetType { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>e.g. VIN, address, or serial number — free-form per asset type.</summary>
    public string? Identifier { get; set; }
}
