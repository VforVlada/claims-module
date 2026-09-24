using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Persistence.Seed;

// PolicyCoverage seed rows live as raw SQL in the InitialCreate migration instead of here —
// EF Core's HasData() doesn't support entities with complex properties (dotnet/efcore#31254).

internal static class PolicySeed
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly Guid Policy1Id = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Policy2Id = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    public static readonly Guid Policy3Id = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    public static readonly Guid Policy4Id = Guid.Parse("00000000-0000-0000-0000-0000000000a4");

    public static readonly Policy[] Policies =
    [
        new()
        {
            Id = Policy1Id,
            OrganizationEntityId = SeedIds.DefaultOrganization,
            PolicyNumber = "POL-2026-000101",
            ClientName = "Acme Logistics LLC",
            EffectiveDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ExpirationDate = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = SeededAt,
            UserCreated = "seed"
        },
        new()
        {
            Id = Policy2Id,
            OrganizationEntityId = SeedIds.DefaultOrganization,
            PolicyNumber = "POL-2025-000202",
            ClientName = "Harborview Property Group",
            EffectiveDate = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ExpirationDate = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = SeededAt,
            UserCreated = "seed"
        },
        new()
        {
            Id = Policy3Id,
            OrganizationEntityId = SeedIds.DefaultOrganization,
            PolicyNumber = "POL-2024-000303",
            ClientName = "Meridian Retail Partners",
            EffectiveDate = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero),
            ExpirationDate = new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedAt = SeededAt,
            UserCreated = "seed"
        },
        // Narrow one-week window — exercises the inclusive effective/expiration boundaries (BR-C-02).
        new()
        {
            Id = Policy4Id,
            OrganizationEntityId = SeedIds.DefaultOrganization,
            PolicyNumber = "POL-2026-000404",
            ClientName = "Short-Term Events Co",
            EffectiveDate = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            ExpirationDate = new DateTimeOffset(2026, 9, 7, 23, 59, 59, TimeSpan.Zero),
            CreatedAt = SeededAt,
            UserCreated = "seed"
        }
    ];
}
