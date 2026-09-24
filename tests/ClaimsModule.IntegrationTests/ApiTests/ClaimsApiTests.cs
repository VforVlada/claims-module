using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;

namespace ClaimsModule.IntegrationTests.ApiTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ClaimsApiTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>I-API-01: POST a valid claim → 201, Location header, claim number, CLAIM_CREATED audit row.</summary>
    [Fact]
    public async Task Create_ValidClaim_Returns201WithLocationClaimNumberAndAuditRow()
    {
        var client = Factory.CreateHandlerClient();

        var response = await client.PostAsJsonAsync("/api/claims", new ClaimBuilder().BuildCommand());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<ResultEnvelope<ClaimDetailDto>>())!.Value;
        Assert.Matches(@"^CLM-\d{4}-\d{7}$", created.ClaimNumber);
        Assert.Equal($"/api/claims/{created.Id}", response.Headers.Location!.AbsolutePath, ignoreCase: true);
        Assert.Contains(await client.GetAuditAsync(created.Id), a => a.Action == "CLAIM_CREATED" && a.NewValues == created.ClaimNumber);
    }

    /// <summary>I-API-02 / FNOL-02: outside the policy period is a warning, not a failure — and it's audited.</summary>
    [Fact]
    public async Task Create_LossDateOutsidePolicyPeriod_Returns201WithWarningAndAuditRow()
    {
        var client = Factory.CreateHandlerClient();

        var response = await client.PostAsJsonAsync("/api/claims", new ClaimBuilder().WithPolicy(Seeded.ExpiredPolicy).BuildCommand());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var envelope = (await response.Content.ReadFromJsonAsync<ResultEnvelope<ClaimDetailDto>>())!;
        var warning = Assert.Single(envelope.Warnings);
        Assert.Equal("BR-C-02", warning.Code);
        Assert.Contains(await client.GetAuditAsync(envelope.Value.Id), a => a.Action == "CLAIM_WARNING" && a.NewValues!.StartsWith("BR-C-02"));
    }

    /// <summary>U-C-04 end to end against the seeded narrow-window policy: both bounds are inclusive.</summary>
    [Theory]
    [InlineData("2026-09-01T00:00:00+00:00", 0)]
    [InlineData("2026-09-07T23:59:59+00:00", 0)]
    [InlineData("2026-08-31T23:59:59+00:00", 1)]
    [InlineData("2026-09-08T00:00:00+00:00", 1)]
    public async Task Create_NarrowWindowPolicy_BoundsAreInclusive(string lossDate, int expectedWarnings)
    {
        var command = new ClaimBuilder().WithPolicy(Seeded.NarrowWindowPolicy).WithLossDate(DateTimeOffset.Parse(lossDate)).BuildCommand();

        var response = await Factory.CreateHandlerClient().PostAsJsonAsync("/api/claims", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(expectedWarnings, (await response.Content.ReadFromJsonAsync<ResultEnvelope<ClaimDetailDto>>())!.Warnings.Count);
    }

    /// <summary>I-API-03: business validation failures → 400 ProblemDetails with errors keyed per field.</summary>
    [Fact]
    public async Task Create_InvalidClaim_Returns400ProblemDetailsWithFieldErrors()
    {
        var command = new ClaimBuilder()
            .WithLossDate(DateTimeOffset.UtcNow.AddDays(1))
            .WithCauseOfLoss(Seeded.InactiveCode)
            .WithParties(new ClaimPartyInput(PartyType.Individual, PartyRole.Witness, "Walter Witness", null, null))
            .BuildCommand();

        var response = await Factory.CreateHandlerClient().PostAsJsonAsync("/api/claims", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        var errors = body.RootElement.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        Assert.Contains("LossDate", errors);
        Assert.Contains("CauseOfLossCodeId", errors);
        Assert.Contains("Parties", errors);
    }

    /// <summary>Structurally broken JSON is rejected by model binding — still 400, never a 500.</summary>
    [Fact]
    public async Task Create_MalformedJsonBody_Returns400()
    {
        using var content = new StringContent(
            """{ "claimType": "NotARealClaimType", "lossDate": "2026-01-01", "lossDescription": "x", "lossLocation": "x", "causeOfLossCodeId": "00000000-0000-0000-0000-0000000000c1", "assignedHandler": "x", "parties": [], "riskObjects": [] }""",
            System.Text.Encoding.UTF8, "application/json");

        var response = await Factory.CreateHandlerClient().PostAsync("/api/claims", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>I-API-04: 20 parallel POSTs → 20 unique claim numbers, no errors (the SQL SEQUENCE is the guard).</summary>
    [Fact]
    public async Task Create_TwentyParallelPosts_AllSucceedWithUniqueClaimNumbers()
    {
        var client = Factory.CreateHandlerClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => client.PostAsJsonAsync("/api/claims", new ClaimBuilder().BuildCommand())));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        var numbers = await Task.WhenAll(responses.Select(async r => (await r.Content.ReadFromJsonAsync<ResultEnvelope<ClaimDetailDto>>())!.Value.ClaimNumber));
        Assert.Equal(20, numbers.Distinct().Count());
    }

    /// <summary>I-API-05: each filter alone, combined, and paging with totalCount.</summary>
    [Fact]
    public async Task List_FiltersAloneAndCombined_ReturnCorrectSubsetAndTotalCount()
    {
        var older = DateTimeOffset.UtcNow.AddDays(-30);
        await Factory.SeedClaimAsync(new ClaimBuilder().AssignedTo("Alice Adjuster").WithLossDate(older));
        await Factory.SeedClaimAsync(new ClaimBuilder().AssignedTo("Alice Adjuster").WithCauseOfLoss(Seeded.FireCode).InStatus(ClaimStatus.Open));
        await Factory.SeedClaimAsync(new ClaimBuilder().AssignedTo("Bob Broker").InStatus(ClaimStatus.Open));
        for (var i = 0; i < 4; i++)
        {
            await Factory.SeedClaimAsync(new ClaimBuilder().AssignedTo("Carol Clerk"));
        }

        var client = Factory.CreateHandlerClient();
        async Task<PagedResponse<ClaimListItemDto>> List(string query) =>
            (await client.GetFromJsonAsync<PagedResponse<ClaimListItemDto>>($"/api/claims?{query}"))!;

        Assert.Equal(7, (await List("")).TotalCount);
        Assert.Equal(2, (await List($"statuses={(int)ClaimStatus.Open}")).TotalCount);
        Assert.Equal(2, (await List("assignedHandler=Alice")).TotalCount);
        Assert.Equal(1, (await List($"causeOfLossCodeId={Seeded.FireCode}")).TotalCount);
        Assert.Equal(1, (await List($"toDate={Uri.EscapeDataString(older.AddDays(1).ToString("O"))}")).TotalCount);
        Assert.Equal(6, (await List($"fromDate={Uri.EscapeDataString(older.AddDays(1).ToString("O"))}")).TotalCount);

        var combined = await List($"statuses={(int)ClaimStatus.Open}&assignedHandler=Alice");
        Assert.Equal("Alice Adjuster", Assert.Single(combined.Items).AssignedHandler);

        var page1 = await List("pageSize=3&pageNumber=1");
        var page2 = await List("pageSize=3&pageNumber=2");
        var page3 = await List("pageSize=3&pageNumber=3");
        Assert.Equal(7, page2.TotalCount);
        Assert.Equal(3, page2.Items.Count);
        Assert.Single(page3.Items);
        Assert.Equal(7, page1.Items.Concat(page2.Items).Concat(page3.Items).Select(c => c.Id).Distinct().Count());
    }

    /// <summary>An inverted date range is a bad filter (400 with a field error), not "no claims".</summary>
    [Fact]
    public async Task List_ToDateBeforeFromDate_Returns400()
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));

        var response = await Factory.CreateHandlerClient().GetAsync($"/api/claims?fromDate={from}&toDate={to}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ToDate", await response.Content.ReadAsStringAsync());
    }

    /// <summary>I-API-06: detail carries the whole aggregate.</summary>
    [Fact]
    public async Task GetById_ReturnsPartiesRiskObjectsAndReserves()
    {
        var client = Factory.CreateHandlerClient();
        var created = await client.CreateClaimAsync(new ClaimBuilder()
            .WithRiskObject(AssetType.Vehicle, "2021 Toyota Camry", "VIN123")
            .WithInitialReserve(5000m));

        var detail = await client.GetFromJsonAsync<ClaimDetailDto>($"/api/claims/{created.Id}");

        Assert.Single(detail!.Parties);
        Assert.Equal("VIN123", Assert.Single(detail.RiskObjects).Identifier);
        var reserve = Assert.Single(detail.ReserveComponents);
        Assert.Equal(5000m, reserve.CurrentAmount);
        Assert.Single(reserve.History);
    }

    /// <summary>I-API-07 / WF-02: an illegal transition → 409 (documented convention) listing the legal next statuses.</summary>
    [Fact]
    public async Task TransitionStatus_NotInWorkflow_Returns409WithAllowedNextStatuses()
    {
        var claim = await Factory.SeedClaimAsync(new ClaimBuilder());

        var response = await Factory.CreateHandlerClient().TransitionAsync(claim.Id, ClaimStatus.Closed);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var allowed = body.RootElement.GetProperty("allowedNextStatuses").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Open", allowed);
        Assert.DoesNotContain("Closed", allowed);
    }

    /// <summary>I-API-08 / WF-05: a real transition the caller's role lacks permission for → 403.</summary>
    [Fact]
    public async Task TransitionStatus_RoleLacksPermission_Returns403()
    {
        var claim = await Factory.SeedClaimAsync(new ClaimBuilder().InStatus(ClaimStatus.Closed));

        var asHandler = await Factory.CreateHandlerClient().TransitionAsync(claim.Id, ClaimStatus.Reopened);
        var asSupervisor = await Factory.CreateSupervisorClient().TransitionAsync(claim.Id, ClaimStatus.Reopened);

        Assert.Equal(HttpStatusCode.Forbidden, asHandler.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asSupervisor.StatusCode);
    }

    [Fact]
    public async Task TransitionStatus_Allowed_UpdatesStatusAndAudits()
    {
        var claim = await Factory.SeedClaimAsync(new ClaimBuilder());
        var client = Factory.CreateHandlerClient();

        var response = await client.TransitionAsync(claim.Id, ClaimStatus.Open);

        response.EnsureSuccessStatusCode();
        Assert.Equal(ClaimStatus.Open, (await response.Content.ReadFromJsonAsync<ClaimDetailDto>())!.Status);
        Assert.Contains(await client.GetAuditAsync(claim.Id), a => a.Action == "STATUS_CHANGED");
    }

    /// <summary>I-API-09: newest first.</summary>
    [Fact]
    public async Task GetAuditLog_ReturnsEntriesInReverseChronologicalOrder()
    {
        var client = Factory.CreateHandlerClient();
        var created = await client.CreateClaimAsync(new ClaimBuilder().WithInitialReserve(5000m));
        (await client.TransitionAsync(created.Id, ClaimStatus.Open)).EnsureSuccessStatusCode();

        var audit = await client.GetAuditAsync(created.Id);

        Assert.True(audit.Count >= 3, $"expected several audit entries, got {audit.Count}");
        Assert.Equal(audit.OrderByDescending(a => a.CreatedAt).Select(a => a.Id), audit.Select(a => a.Id));
    }

    /// <summary>I-API-13: unknown id or route → 404 ProblemDetails, and never a stack trace.</summary>
    [Theory]
    [InlineData("/api/claims/{0}")]
    [InlineData("/api/no-such-route")]
    public async Task UnknownIdOrRoute_Returns404ProblemDetailsWithoutStackTrace(string pathTemplate)
    {
        var response = await Factory.CreateHandlerClient().GetAsync(string.Format(pathTemplate, Guid.NewGuid()));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, JsonDocument.Parse(body).RootElement.GetProperty("status").GetInt32());
        Assert.DoesNotContain("StackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" at ClaimsModule.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousRequest_Returns401()
    {
        var response = await Factory.CreateClient().GetAsync("/api/claims");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
