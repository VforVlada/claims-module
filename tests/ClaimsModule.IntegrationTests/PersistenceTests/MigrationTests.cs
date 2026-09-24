using ClaimsModule.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.IntegrationTests.PersistenceTests;

/// <summary>
/// I-DB-01: ApiWebApplicationFactory applies every migration to an empty SQL Server database in
/// InitializeAsync (that is the "succeeds" half); these assert nothing is left over — no
/// unapplied migration, and no entity/configuration change that lacks a migration.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class MigrationTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Migrate_EmptyDatabase_LeavesNoPendingMigrations()
    {
        await using var context = Factory.CreateDbContext();

        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task Model_HasNoChangesMissingAMigration()
    {
        await using var context = Factory.CreateDbContext();

        Assert.False(context.Database.HasPendingModelChanges(), "The EF model has changes that no migration captures — run 'dotnet ef migrations add'.");
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }
}
