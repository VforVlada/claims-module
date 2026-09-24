namespace ClaimsModule.Persistence.Seed;

/// <summary>Fixed GUIDs for migration-time HasData() seeding — must stay stable across migrations.</summary>
internal static class SeedIds
{
    public static readonly Guid DefaultOrganization = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Second tenant — exists only so tenant-isolation behavior can be tested and demoed.</summary>
    public static readonly Guid SecondOrganization = Guid.Parse("22222222-2222-2222-2222-222222222222");
}
