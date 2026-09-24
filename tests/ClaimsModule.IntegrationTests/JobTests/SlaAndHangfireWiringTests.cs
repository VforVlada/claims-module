using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.IntegrationTests.Infrastructure;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.JobTests;

/// <summary>Plan section 4.3, SLA monitoring (I-JOB-06..09) and Hangfire registration/exposure (I-JOB-10, I-JOB-11).</summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SlaAndHangfireWiringTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string SlaAction = "SLA_BREACH_DETECTED";

    private async Task<Guid> SeedStaleClaimAsync(ClaimStatus status, TimeSpan untouchedFor, Guid? organizationId = null)
    {
        var builder = new ClaimBuilder().InStatus(status);
        if (organizationId is not null)
        {
            builder.ForOrganization(organizationId.Value).WithCauseOfLoss(Seeded.OrgBCode);
        }

        var claim = await Factory.SeedClaimAsync(builder);
        var lastTouched = DateTimeOffset.UtcNow - untouchedFor;
        await using var context = Factory.CreateDbContext(builder.OrganizationId);
        await context.Claims.IgnoreQueryFilters().Where(c => c.Id == claim.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, lastTouched).SetProperty(c => c.UpdatedAt, lastTouched));
        return claim.Id;
    }

    private async Task RunSlaJobAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SlaMonitoringJob>().ExecuteAsync();
    }

    private async Task<int> BreachEntriesAsync(Guid claimId, Guid? organizationId = null)
    {
        await using var context = Factory.CreateDbContext(organizationId);
        return await context.ClaimAuditLogs.CountAsync(a => a.ClaimId == claimId && a.Action == SlaAction);
    }

    /// <summary>I-JOB-06: a Draft claim untouched for 49h is flagged exactly once.</summary>
    [Fact]
    public async Task DraftClaimUpdated49HoursAgo_IsFlaggedOnce()
    {
        var claimId = await SeedStaleClaimAsync(ClaimStatus.Draft, TimeSpan.FromHours(49));

        await RunSlaJobAsync();

        Assert.Equal(1, await BreachEntriesAsync(claimId));
    }

    /// <summary>
    /// §3.5: the claim itself carries the SlaBreached flag, visible through the API — and flagging
    /// is bookkeeping, not activity, so it must not move UpdatedAt (the clock the SLA measures).
    /// </summary>
    [Fact]
    public async Task StaleClaim_IsFlaggedSlaBreachedWithoutResettingItsActivityClock()
    {
        var claimId = await SeedStaleClaimAsync(ClaimStatus.Open, TimeSpan.FromHours(50));
        DateTimeOffset? updatedBefore;
        await using (var context = Factory.CreateDbContext())
        {
            updatedBefore = (await context.Claims.AsNoTracking().SingleAsync(c => c.Id == claimId)).UpdatedAt;
        }

        await RunSlaJobAsync();

        var detail = await Factory.CreateHandlerClient().GetFromJsonAsync<Application.Claims.Dtos.ClaimDetailDto>($"/api/claims/{claimId}");
        var list = await Factory.CreateHandlerClient().GetFromJsonAsync<PagedResponse<Application.Claims.Dtos.ClaimListItemDto>>("/api/claims");
        Assert.True(detail!.IsSlaBreached);
        Assert.NotNull(detail.SlaBreachedAt);
        Assert.True(list!.Items.Single(c => c.Id == claimId).IsSlaBreached);
        await using var verify = Factory.CreateDbContext();
        Assert.Equal(updatedBefore, (await verify.Claims.AsNoTracking().SingleAsync(c => c.Id == claimId)).UpdatedAt);
    }

    /// <summary>Activity on a flagged claim clears the flag.</summary>
    [Fact]
    public async Task FlaggedClaim_TransitionClearsTheFlag()
    {
        var claimId = await SeedStaleClaimAsync(ClaimStatus.Open, TimeSpan.FromHours(50));
        await RunSlaJobAsync();

        (await Factory.CreateHandlerClient().TransitionAsync(claimId, ClaimStatus.UnderInvestigation)).EnsureSuccessStatusCode();

        var detail = await Factory.CreateHandlerClient().GetFromJsonAsync<Application.Claims.Dtos.ClaimDetailDto>($"/api/claims/{claimId}");
        Assert.False(detail!.IsSlaBreached);
    }

    /// <summary>I-JOB-07: 47h is inside the 48h window.</summary>
    [Fact]
    public async Task OpenClaimUpdated47HoursAgo_IsNotFlagged()
    {
        var claimId = await SeedStaleClaimAsync(ClaimStatus.Open, TimeSpan.FromHours(47));

        await RunSlaJobAsync();

        Assert.Equal(0, await BreachEntriesAsync(claimId));
    }

    /// <summary>I-JOB-08: the SLA only covers Draft/Open — an UnderInvestigation claim is being worked.</summary>
    [Fact]
    public async Task UnderInvestigationClaimUpdated72HoursAgo_IsNotFlagged()
    {
        var claimId = await SeedStaleClaimAsync(ClaimStatus.UnderInvestigation, TimeSpan.FromHours(72));

        await RunSlaJobAsync();

        Assert.Equal(0, await BreachEntriesAsync(claimId));
    }

    /// <summary>I-JOB-09: idempotent across runs (the 15-minute schedule must not re-flag).</summary>
    [Fact]
    public async Task RunTwice_NoDuplicateFlag()
    {
        var claimId = await SeedStaleClaimAsync(ClaimStatus.Open, TimeSpan.FromHours(60));

        await RunSlaJobAsync();
        await RunSlaJobAsync();

        Assert.Equal(1, await BreachEntriesAsync(claimId));
    }

    /// <summary>The job is cross-tenant by design: it runs with no user, yet flags every organization's claims under that organization.</summary>
    [Fact]
    public async Task StaleClaimsInEveryOrganization_AreFlaggedUnderTheirOwnTenant()
    {
        var orgAClaim = await SeedStaleClaimAsync(ClaimStatus.Draft, TimeSpan.FromHours(50));
        var orgBClaim = await SeedStaleClaimAsync(ClaimStatus.Draft, TimeSpan.FromHours(50), ApiWebApplicationFactory.OrgBId);

        await RunSlaJobAsync();

        Assert.Equal(1, await BreachEntriesAsync(orgAClaim));
        Assert.Equal(1, await BreachEntriesAsync(orgBClaim, ApiWebApplicationFactory.OrgBId));
    }

    /// <summary>I-JOB-10 / JOB-05: startup registers the SLA job to run every 15 minutes.</summary>
    [Fact]
    public void RecurringSlaJob_IsRegisteredEveryFifteenMinutes()
    {
        using var connection = Factory.JobStorage.GetConnection();

        var job = Assert.Single(connection.GetRecurringJobs(), j => j.Id == RecurringJobRegistration.SlaMonitoringJobId);

        Assert.Equal("*/15 * * * *", job.Cron);
        Assert.Equal(typeof(SlaMonitoringJob), job.Job.Type);
    }

    /// <summary>I-JOB-11 / JOB-08: outside Development the dashboard isn't mapped at all — anonymous or not.</summary>
    [Fact]
    public async Task HangfireDashboard_OutsideDevelopment_IsNotExposed()
    {
        var anonymous = await Factory.CreateClient().GetAsync("/hangfire");
        var asManager = await Factory.CreateManagerClient().GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asManager.StatusCode);
    }
}
