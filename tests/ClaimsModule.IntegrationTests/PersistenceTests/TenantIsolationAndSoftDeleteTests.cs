using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.IntegrationTests.PersistenceTests;

/// <summary>
/// I-DB-05..07: the global query filters (soft delete + tenant) enforced by the real
/// ClaimsDbContext on real SQL Server, observed through the API.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class TenantIsolationAndSoftDeleteTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>I-DB-06: another tenant's claim is indistinguishable from a missing one.</summary>
    [Fact]
    public async Task GetById_ClaimBelongsToAnotherOrganization_Returns404()
    {
        var orgAClaim = await Factory.SeedClaimAsync(new ClaimBuilder());

        var response = await Factory.CreateOrgBClient().GetAsync($"/api/claims/{orgAClaim.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>I-DB-07.</summary>
    [Fact]
    public async Task List_ClaimsFromMultipleOrganizations_OnlyReturnsCallersOwnOrganization()
    {
        await Factory.SeedClaimAsync(new ClaimBuilder());
        await Factory.SeedClaimAsync(new ClaimBuilder());
        var orgBClaim = await Factory.SeedClaimAsync(new ClaimBuilder().ForOrganization(ApiWebApplicationFactory.OrgBId).WithCauseOfLoss(Seeded.OrgBCode));

        var page = await Factory.CreateOrgBClient().GetFromJsonAsync<PagedResponse<ClaimListItemDto>>("/api/claims");

        var item = Assert.Single(page!.Items);
        Assert.Equal(orgBClaim.Id, item.Id);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task OrgBUser_CannotReadOrWriteOrgAClaimThroughAnyEndpoint()
    {
        var orgAClaim = await Factory.SeedClaimAsync(new ClaimBuilder());
        var orgB = Factory.CreateOrgBClient();

        Assert.Equal(HttpStatusCode.NotFound, (await orgB.TransitionAsync(orgAClaim.Id, ClaimStatus.Open)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await orgB.PostAsJsonAsync($"/api/claims/{orgAClaim.Id}/reserves", new ReserveBuilder().BuildRequest())).StatusCode);
        Assert.Empty(await orgB.GetAuditAsync(orgAClaim.Id));
    }

    /// <summary>U-C-10 end to end: Org A can't file a claim against a cause-of-loss code owned by Org B.</summary>
    [Fact]
    public async Task Create_WithAnotherOrganizationsCauseOfLossCode_Returns400()
    {
        var response = await Factory.CreateHandlerClient().PostAsJsonAsync("/api/claims", new ClaimBuilder().WithCauseOfLoss(Seeded.OrgBCode).BuildCommand());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>I-DB-05: a soft-deleted party is invisible to reads without being physically removed.</summary>
    [Fact]
    public async Task SoftDeletedParty_IsExcludedFromClaimDetail()
    {
        var claim = await Factory.SeedClaimAsync(new ClaimBuilder().WithParties(
            new ClaimPartyInput(PartyType.Individual, PartyRole.Claimant, "Claire Claimant", null, null),
            new ClaimPartyInput(PartyType.Individual, PartyRole.Witness, "Walter Witness", null, null)));
        await using (var context = Factory.CreateDbContext())
        {
            var witness = await context.ClaimParties.SingleAsync(p => p.ClaimId == claim.Id && p.PartyRole == PartyRole.Witness);
            witness.MarkDeleted(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        var detail = await Factory.CreateHandlerClient().GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{claim.Id}");

        var party = Assert.Single(detail!.Parties);
        Assert.Equal("Claire Claimant", party.Name);
        await using var verify = Factory.CreateDbContext();
        Assert.Equal(2, await verify.ClaimParties.IgnoreQueryFilters().CountAsync(p => p.ClaimId == claim.Id));
    }
}
