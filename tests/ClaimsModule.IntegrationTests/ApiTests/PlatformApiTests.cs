using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaimsModule.Application.Policies.Dtos;
using ClaimsModule.Application.ReferenceData.Dtos;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.ApiTests;

/// <summary>Reference data, Swagger and health: the endpoints the smoke stage (plan section 7) relies on.</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class PlatformApiTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>I-API-12: cause-of-loss codes filter by peril and hide inactive / other-tenant codes.</summary>
    [Fact]
    public async Task CauseOfLossCodes_FilterByPeril_AndExcludeInactiveAndForeignCodes()
    {
        var client = Factory.CreateHandlerClient();

        var all = (await client.GetFromJsonAsync<List<CauseOfLossCodeDto>>("/api/reference/cause-of-loss-codes"))!;
        var property = (await client.GetFromJsonAsync<List<CauseOfLossCodeDto>>("/api/reference/cause-of-loss-codes?perilCategory=Property"))!;

        Assert.Equal(9, all.Count);
        Assert.DoesNotContain(all, c => c.Id == Seeded.InactiveCode || c.Id == Seeded.OrgBCode);
        Assert.NotEmpty(property);
        Assert.All(property, c => Assert.Equal("Property", c.PerilCategory));
    }

    /// <summary>I-API-12: claim statuses come with the caller's allowed next statuses.</summary>
    [Fact]
    public async Task ClaimStatuses_IncludeTransitionsForCallersRole()
    {
        var handlerView = (await Factory.CreateHandlerClient().GetFromJsonAsync<List<ClaimStatusDto>>("/api/reference/claim-statuses"))!;
        var supervisorView = (await Factory.CreateSupervisorClient().GetFromJsonAsync<List<ClaimStatusDto>>("/api/reference/claim-statuses"))!;

        Assert.Equal(7, handlerView.Count);
        Assert.Equal([ClaimStatus.Open, ClaimStatus.Withdrawn], handlerView.Single(s => s.Status == ClaimStatus.Draft).AllowedNextStatuses.Order());
        Assert.Empty(handlerView.Single(s => s.Status == ClaimStatus.Closed).AllowedNextStatuses);
        Assert.Equal([ClaimStatus.Reopened], supervisorView.Single(s => s.Status == ClaimStatus.Closed).AllowedNextStatuses);
    }

    /// <summary>I-API-14 / S-02: Swagger generates, anonymously, and documents every routed action.</summary>
    [Fact]
    public async Task SwaggerJson_Generates_WithEveryEndpoint()
    {
        var response = await Factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        response.EnsureSuccessStatusCode();
        using var swagger = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var documented = swagger.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject().Select(op => $"{op.Name.ToUpperInvariant()} /{p.Name.TrimStart('/')}"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var routed = Factory.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>().ApiDescriptionGroups.Items
            .SelectMany(g => g.Items)
            .Select(d => $"{d.HttpMethod} /{d.RelativePath}")
            .ToList();

        Assert.True(routed.Count >= 15, $"only {routed.Count} routed actions found");
        var missing = routed.Where(r => !documented.Contains(StripConstraints(r))).ToList();
        Assert.Empty(missing);
    }

    private static string StripConstraints(string route) =>
        System.Text.RegularExpressions.Regex.Replace(route, @"\{(\w+):[^}]+\}", "{$1}");

    /// <summary>The FNOL policy typeahead: matches by number or client, reports in-force status, and exposes coverages.</summary>
    [Fact]
    public async Task PolicySearchAndCoverage_ReturnSeededPolicies()
    {
        var client = Factory.CreateHandlerClient();

        var results = (await client.GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?searchTerm=Acme"))!;
        var coverage = (await client.GetFromJsonAsync<List<PolicyCoverageDto>>($"/api/policies/{Seeded.ActivePolicy}/coverage"))!;
        var orgBResults = (await Factory.CreateOrgBClient().GetFromJsonAsync<List<PolicySearchResultDto>>("/api/policies/search?searchTerm=POL"))!;

        var acme = Assert.Single(results);
        Assert.Equal(Seeded.ActivePolicy, acme.Id);
        Assert.True(acme.IsInForce);
        Assert.NotEmpty(coverage);
        Assert.All(coverage, c => Assert.True(c.Limit > 0));
        Assert.Empty(orgBResults);
    }

    [Fact]
    public async Task AddPartyAndListReserves_RoundTrip()
    {
        var client = Factory.CreateHandlerClient();
        var claim = await client.CreateClaimAsync(new ClaimBuilder().WithInitialReserve(2500m));

        var added = await client.PostAsJsonAsync($"/api/claims/{claim.Id}/parties", new { PartyType = PartyType.Individual, PartyRole = PartyRole.Witness, Name = "Walter Witness" });
        var invalid = await client.PostAsJsonAsync($"/api/claims/{claim.Id}/parties", new { PartyType = PartyType.Individual, PartyRole = PartyRole.Witness, Name = "" });
        var reserves = (await client.GetFromJsonAsync<List<Application.Reserves.Dtos.ReserveComponentDto>>($"/api/claims/{claim.Id}/reserves"))!;

        await ApiClientExtensions.EnsureSuccessWithBodyAsync(added);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(2500m, Assert.Single(reserves).CurrentAmount);
        Assert.Contains(await client.GetAuditAsync(claim.Id), a => a.Action == "PARTY_ADDED");
    }

    /// <summary>S-01 locally: /health checks the database and blob storage (Azurite here).</summary>
    [Fact]
    public async Task Health_WithDatabaseAndStorageReachable_Returns200()
    {
        var response = await Factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
