using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.IntegrationTests.PersistenceTests;

/// <summary>
/// I-DB-08: two contexts update the same claim; the second save fails. The RowVersion shadow property
/// (IsRowVersion()) is what makes EF include the original row version in the second UPDATE's
/// WHERE clause, so it affects zero rows and EF raises DbUpdateConcurrencyException. See
/// ExceptionHandlingMiddlewareTests for the "API returns 409" half of this test id.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ConcurrencyTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task SaveChanges_TwoContextsUpdateSameClaim_SecondSaveThrowsConcurrencyException()
    {
        var seeded = await Factory.SeedClaimAsync(new ClaimBuilder());

        await using var context1 = Factory.CreateDbContext();
        await using var context2 = Factory.CreateDbContext();

        // Both load the same row (same RowVersion) before either writes. Transitioning status
        // (not e.g. adding a child party) is what actually touches the Claims row itself, which
        // is where RowVersion lives.
        var claim1 = await context1.Claims.Include(c => c.Parties).SingleAsync(c => c.Id == seeded.Id);
        var claim2 = await context2.Claims.Include(c => c.Parties).SingleAsync(c => c.Id == seeded.Id);

        claim1.TransitionTo(ClaimStatus.Open, "first writer");
        await context1.SaveChangesAsync();

        claim2.TransitionTo(ClaimStatus.Withdrawn, "second writer");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => context2.SaveChangesAsync());
    }
}
