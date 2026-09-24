using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.IntegrationTests.Infrastructure;
using ClaimsModule.Persistence;
using Hangfire;
using Hangfire.AspNetCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.JobTests;

/// <summary>
/// Plan section 4.3, GL posting. The job class is resolved from the app's own container and run
/// exactly as Hangfire runs it: in a DI scope with no HTTP request, hence no current user — so
/// these tests also prove the job reaches past the tenant query filter.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class GlPostingJobTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string GlAction = "GL_POSTING_SIMULATED";

    private async Task<ReserveComponentDto> OpenAutoApprovedReserveAsync(decimal amount = 5000m)
    {
        var handler = Factory.CreateHandlerClient();
        var claim = await handler.CreateClaimAsync();
        return await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().WithAmount(amount));
    }

    private async Task RunJobAsync(ReserveComponentDto reserve, Guid historyId)
    {
        using var scope = Factory.Services.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<PostGlReserveChangeJob>();
        await job.ExecuteAsync(historyId, reserve.ClaimId, reserve.Id);
    }

    private async Task<List<Domain.Entities.ClaimAuditLog>> GlEntriesAsync(Guid claimId)
    {
        await using var context = Factory.CreateDbContext();
        return await context.ClaimAuditLogs.AsNoTracking().Where(a => a.ClaimId == claimId && a.Action == GlAction).ToListAsync();
    }

    /// <summary>I-JOB-01: one run → one audit entry with DR/CR lines, amount and idempotency key; history marked Posted.</summary>
    [Fact]
    public async Task RunOnce_WritesOneJournalEntryAndMarksPosted()
    {
        var reserve = await OpenAutoApprovedReserveAsync(5000m);
        var history = reserve.History.Single();

        await RunJobAsync(reserve, history.Id);

        var entry = Assert.Single(await GlEntriesAsync(reserve.ClaimId));
        Assert.StartsWith("DR Change in Outstanding Reserves 5,000.0000 USD / CR Outstanding Loss Reserves 5,000.0000 USD", entry.NewValues);
        Assert.Equal($"Reserve:{reserve.Id}:Change:1", entry.IdempotencyKey);
        Assert.Equal(ApiWebApplicationFactory.DefaultOrganizationId, entry.OrganizationEntityId);
        await using var context = Factory.CreateDbContext();
        Assert.Equal(PostingStatus.Posted, (await context.ReserveHistories.SingleAsync(h => h.Id == history.Id)).PostingStatus);

        // The entry is stamped with the claim's tenant, so the claim's own org can see it.
        Assert.Contains(await Factory.CreateHandlerClient().GetAuditAsync(reserve.ClaimId), a => a.Action == GlAction);
    }

    /// <summary>I-JOB-02: re-running (a Hangfire retry) with the same key is a no-op.</summary>
    [Fact]
    public async Task RunTwiceInSequence_StillOneEntry()
    {
        var reserve = await OpenAutoApprovedReserveAsync();
        var historyId = reserve.History.Single().Id;

        await RunJobAsync(reserve, historyId);
        await RunJobAsync(reserve, historyId);

        Assert.Single(await GlEntriesAsync(reserve.ClaimId));
    }

    /// <summary>
    /// I-JOB-03: two truly concurrent executions. Both can pass the "already posted?" read before
    /// either commits; the unique index on ClaimAuditLog.IdempotencyKey is what guarantees a
    /// single entry, and the loser must exit cleanly rather than fail the job.
    /// </summary>
    [Fact]
    public async Task RunTwiceInParallel_OneEntry_LoserExitsCleanly()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var reserve = await OpenAutoApprovedReserveAsync();
            var historyId = reserve.History.Single().Id;

            await Task.WhenAll(RunJobAsync(reserve, historyId), RunJobAsync(reserve, historyId));

            Assert.Single(await GlEntriesAsync(reserve.ClaimId));
        }
    }

    /// <summary>I-JOB-04: two changes on one reserve are two postings with distinct keys.</summary>
    [Fact]
    public async Task TwoChangesOnOneReserve_TwoEntriesWithDistinctKeys()
    {
        var reserve = await OpenAutoApprovedReserveAsync(5000m);
        var adjust = await Factory.CreateHandlerClient().PutAsJsonAsync($"/api/claims/{reserve.ClaimId}/reserves/{reserve.Id}", new { Amount = 6000m, Reason = "Revised estimate", Currency = "USD" });
        await ApiClientExtensions.EnsureSuccessWithBodyAsync(adjust);
        await using (var context = Factory.CreateDbContext())
        {
            foreach (var history in await context.ReserveHistories.Where(h => h.ReserveComponentId == reserve.Id).ToListAsync())
            {
                await RunJobAsync(reserve, history.Id);
            }
        }

        var entries = await GlEntriesAsync(reserve.ClaimId);

        Assert.Equal(2, entries.Count);
        Assert.Equal([$"Reserve:{reserve.Id}:Change:1", $"Reserve:{reserve.Id}:Change:2"], entries.Select(e => e.IdempotencyKey).Order());
    }

    private sealed class CommitFailsUnitOfWork(ClaimsDbContext context) : IUnitOfWork
    {
        private readonly UnitOfWork _inner = new(context);

        public Task BeginTransactionAsync(CancellationToken cancellationToken) => _inner.BeginTransactionAsync(cancellationToken);

        public async Task CommitTransactionAsync(CancellationToken cancellationToken)
        {
            await _inner.RollbackTransactionAsync(cancellationToken);
            throw new InvalidOperationException("Simulated commit failure.");
        }

        public Task RollbackTransactionAsync(CancellationToken cancellationToken) => _inner.RollbackTransactionAsync(cancellationToken);

        public void OnCommitted(Action action) => _inner.OnCommitted(action);
    }

    /// <summary>I-JOB-05: if the approval transaction rolls back, no job is enqueued and nothing is posted.</summary>
    [Fact]
    public async Task ApprovalTransactionRollsBack_NoJobEnqueuedAndNoGlEntry()
    {
        var handler = Factory.CreateHandlerClient();
        var claim = await handler.CreateClaimAsync();
        var reserve = await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().WithAmount(50_000m));
        var historyId = reserve.History.Single().Id;
        using var failingApp = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddScoped<IUnitOfWork, CommitFailsUnitOfWork>()));
        var supervisor = failingApp.CreateClient();
        supervisor.DefaultRequestHeaders.Authorization = Factory.CreateSupervisorClient().DefaultRequestHeaders.Authorization;

        var response = await supervisor.ApproveAsync(reserve, historyId);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(0, await Factory.CountEnqueuedJobsMentioningAsync(historyId));
        Assert.Empty(await GlEntriesAsync(claim.Id));
        await using var context = Factory.CreateDbContext();
        Assert.Equal(ApprovalStatus.PendingApproval, (await context.ReserveHistories.SingleAsync(h => h.Id == historyId)).ApprovalStatus);
    }

    /// <summary>
    /// Wiring: a real Hangfire server (the app's storage and DI container) picks up the job the
    /// approval enqueued and posts it — the full path from HTTP request to GL entry.
    /// </summary>
    [Fact]
    public async Task ApprovedReserve_IsPostedByARealHangfireServer()
    {
        var handler = Factory.CreateHandlerClient();
        var claim = await handler.CreateClaimAsync();
        var reserve = await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().WithAmount(50_000m));
        (await Factory.CreateSupervisorClient().ApproveAsync(reserve, reserve.History.Single().Id)).EnsureSuccessStatusCode();

        using (new BackgroundJobServer(
            new BackgroundJobServerOptions
            {
                Activator = new AspNetCoreJobActivator(Factory.Services.GetRequiredService<IServiceScopeFactory>()),
                WorkerCount = 1,
                SchedulePollingInterval = TimeSpan.FromMilliseconds(200)
            },
            Factory.JobStorage))
        {
            var deadline = DateTime.UtcNow.AddSeconds(30);
            while ((await GlEntriesAsync(claim.Id)).Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(250);
            }
        }

        Assert.Single(await GlEntriesAsync(claim.Id));
    }
}
