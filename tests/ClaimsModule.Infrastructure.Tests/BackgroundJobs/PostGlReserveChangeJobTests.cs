using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.Infrastructure.Tests.TestHelpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Infrastructure.Tests.BackgroundJobs;

public class PostGlReserveChangeJobTests
{
    private static async Task<(Guid ClaimId, Guid ComponentId, Guid HistoryId)> SeedApprovedReserve(TestDbContext context)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "tester");
        context.Claims.Add(claim);
        await context.SaveChangesAsync(CancellationToken.None);
        return (claim.Id, component.Id, component.History.Single().Id);
    }

    [Fact]
    public async Task ExecuteAsync_PendingPosting_MarksPostedAndWritesAuditEntry()
    {
        using var context = TestDbContext.Create();
        var (claimId, componentId, historyId) = await SeedApprovedReserve(context);
        var auditLog = new TestAuditLogService(context, new FakeDateTimeProvider());
        var sut = new PostGlReserveChangeJob(context, auditLog);

        await sut.ExecuteAsync(historyId, claimId, componentId);

        var history = await context.ReserveHistories.SingleAsync(h => h.Id == historyId);
        Assert.Equal(PostingStatus.Posted, history.PostingStatus);
        Assert.Equal("local", history.PostingJobId);

        var auditEntries = await context.ClaimAuditLogs.Where(a => a.ClaimId == claimId && a.Action == "GL_POSTING_SIMULATED").ToListAsync();
        Assert.Single(auditEntries);
    }

    [Fact]
    public async Task ExecuteAsync_RunTwiceWithSameIdempotencyKey_DoesNotDuplicateAuditEntry()
    {
        using var context = TestDbContext.Create();
        var (claimId, componentId, historyId) = await SeedApprovedReserve(context);
        var auditLog = new TestAuditLogService(context, new FakeDateTimeProvider());
        var sut = new PostGlReserveChangeJob(context, auditLog);

        await sut.ExecuteAsync(historyId, claimId, componentId);
        await sut.ExecuteAsync(historyId, claimId, componentId);

        var auditEntries = await context.ClaimAuditLogs.Where(a => a.ClaimId == claimId && a.Action == "GL_POSTING_SIMULATED").ToListAsync();
        Assert.Single(auditEntries);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrentExecutionsRaceOnSameHistory_OnlyOneAuditEntryPersists()
    {
        // Two independent DbContext instances sharing one SQLite connection, standing in for
        // two Hangfire executions of the same reserveHistoryId (an automatic retry racing the
        // original run). Both read the NotPosted row before either writes, so the top-of-method
        // PostingStatus check alone would not stop this — the unique index on
        // ClaimAuditLog.IdempotencyKey is what actually prevents the duplicate (I-JOB-03).
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }
        var options = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options;
        var dateTimeProvider = new FakeDateTimeProvider();

        using var setupContext = new TestDbContext(options, dateTimeProvider);
        setupContext.Database.EnsureCreated();
        var (claimId, componentId, historyId) = await SeedApprovedReserve(setupContext);

        using var context1 = new TestDbContext(options, dateTimeProvider);
        using var context2 = new TestDbContext(options, dateTimeProvider);
        await context1.ReserveHistories.SingleAsync(h => h.Id == historyId);
        await context2.ReserveHistories.SingleAsync(h => h.Id == historyId);

        var job1 = new PostGlReserveChangeJob(context1, new TestAuditLogService(context1, dateTimeProvider));
        var job2 = new PostGlReserveChangeJob(context2, new TestAuditLogService(context2, dateTimeProvider));

        await job1.ExecuteAsync(historyId, claimId, componentId);
        await job2.ExecuteAsync(historyId, claimId, componentId);

        using var verifyContext = new TestDbContext(options, dateTimeProvider);
        var auditEntries = await verifyContext.ClaimAuditLogs.Where(a => a.ClaimId == claimId && a.Action == "GL_POSTING_SIMULATED").ToListAsync();
        Assert.Single(auditEntries);
        Assert.Equal(PostingStatus.Posted, (await verifyContext.ReserveHistories.SingleAsync(h => h.Id == historyId)).PostingStatus);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownHistoryId_NoOps()
    {
        using var context = TestDbContext.Create();
        var auditLog = new TestAuditLogService(context, new FakeDateTimeProvider());
        var sut = new PostGlReserveChangeJob(context, auditLog);

        await sut.ExecuteAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(await context.ClaimAuditLogs.ToListAsync());
    }
}

public class GlJournalEntryTests
{
    /// <summary>§3.5: an increase posts DR Change in Outstanding Reserves / CR Outstanding Loss Reserves.</summary>
    [Fact]
    public void JournalEntry_ReserveIncrease_DebitsChangeInOutstandingReserves()
    {
        var entry = PostGlReserveChangeJob.JournalEntry(new Money(45000m), Guid.Empty, "Reserve:x:Change:2");

        Assert.StartsWith("DR Change in Outstanding Reserves 45,000.0000 USD / CR Outstanding Loss Reserves 45,000.0000 USD", entry);
        Assert.EndsWith("(Reserve:x:Change:2)", entry);
    }

    /// <summary>A reserve reduction reverses the two lines.</summary>
    [Fact]
    public void JournalEntry_ReserveDecrease_ReversesTheLines()
    {
        var entry = PostGlReserveChangeJob.JournalEntry(new Money(-1500m), Guid.Empty, "k");

        Assert.StartsWith("DR Outstanding Loss Reserves 1,500.0000 USD / CR Change in Outstanding Reserves 1,500.0000 USD", entry);
    }
}
