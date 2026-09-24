using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Persistence.Seed;

internal static class CauseOfLossCodeSeed
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly CauseOfLossCode[] Codes =
    [
        Create("00000000-0000-0000-0000-0000000000c1", "COLL", "Collision", "Auto"),
        Create("00000000-0000-0000-0000-0000000000c2", "COMP", "Comprehensive (non-collision)", "Auto"),
        Create("00000000-0000-0000-0000-0000000000c3", "THEFT", "Theft", "Auto"),
        Create("00000000-0000-0000-0000-0000000000c4", "FIRE", "Fire", "Property"),
        Create("00000000-0000-0000-0000-0000000000c5", "WATER", "Water damage", "Property"),
        Create("00000000-0000-0000-0000-0000000000c6", "WIND", "Windstorm/hail", "Property"),
        Create("00000000-0000-0000-0000-0000000000c7", "SLIP", "Slip and fall", "Liability"),
        Create("00000000-0000-0000-0000-0000000000c8", "PRODLIA", "Product liability", "Liability"),
        Create("00000000-0000-0000-0000-0000000000c9", "OTHER", "Other / unclassified", "Other"),
        // Retired code — kept for historical claims, rejected for new ones (BR-C-05).
        Create("00000000-0000-0000-0000-0000000000ca", "LEGACY", "Legacy / retired peril", "Other", isActive: false),
        // Owned by the second tenant — must never be usable from the default org (BR-C-05).
        Create("00000000-0000-0000-0000-0000000000cb", "B-COLL", "Collision (Org B)", "Auto", SeedIds.SecondOrganization)
    ];

    private static CauseOfLossCode Create(string id, string code, string description, string perilCategory, Guid? organizationId = null, bool isActive = true) => new()
    {
        Id = Guid.Parse(id),
        OrganizationEntityId = organizationId ?? SeedIds.DefaultOrganization,
        Code = code,
        Description = description,
        PerilCategory = perilCategory,
        IsActive = isActive,
        CreatedAt = SeededAt,
        UserCreated = "seed"
    };
}
