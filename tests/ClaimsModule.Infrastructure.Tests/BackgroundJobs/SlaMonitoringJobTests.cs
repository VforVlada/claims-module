using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using ClaimsModule.Infrastructure.BackgroundJobs;
using ClaimsModule.Infrastructure.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Infrastructure.Tests.BackgroundJobs;

public class SlaMonitoringJobTests
{
    private static async Task<Claim> SeedClaimCreatedAt(TestDbContext context, FakeDateTimeProvider dateTimeProvider, DateTimeOffset createdAt, ClaimStatus status = ClaimStatus.Draft)
    {
        dateTimeProvider.UtcNow = createdAt;
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", createdAt.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        if (status != ClaimStatus.Draft)
        {
            claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");
            claim.TransitionTo(status, "tester");
        }

        context.Claims.Add(claim);
        await context.SaveChangesAsync(CancellationToken.None);
        return claim;
    }

    [Fact]
    public async Task ExecuteAsync_ClaimStaleFor48Hours_WritesSlaBreachDetectedEntry()
    {
        var dateTimeProvider = new FakeDateTimeProvider();
        using var context = TestDbContext.CreateInMemory(dateTimeProvider);
        var now = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var claim = await SeedClaimCreatedAt(context, dateTimeProvider, now.AddHours(-50));
        dateTimeProvider.UtcNow = now;
        var sut = new SlaMonitoringJob(context, new TestAuditLogService(context, dateTimeProvider), dateTimeProvider);

        await sut.ExecuteAsync(CancellationToken.None);

        var entries = await context.ClaimAuditLogs.Where(a => a.ClaimId == claim.Id && a.Action == "SLA_BREACH_DETECTED").ToListAsync();
        Assert.Single(entries);
    }

    [Fact]
    public async Task ExecuteAsync_ClaimWithinSlaWindow_DoesNotWriteEntry()
    {
        var dateTimeProvider = new FakeDateTimeProvider();
        using var context = TestDbContext.CreateInMemory(dateTimeProvider);
        var now = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var claim = await SeedClaimCreatedAt(context, dateTimeProvider, now.AddHours(-2));
        dateTimeProvider.UtcNow = now;
        var sut = new SlaMonitoringJob(context, new TestAuditLogService(context, dateTimeProvider), dateTimeProvider);

        await sut.ExecuteAsync(CancellationToken.None);

        Assert.Empty(await context.ClaimAuditLogs.Where(a => a.ClaimId == claim.Id).ToListAsync());
    }

    [Fact]
    public async Task ExecuteAsync_ClosedClaim_NeverFlaggedEvenIfStale()
    {
        var dateTimeProvider = new FakeDateTimeProvider();
        using var context = TestDbContext.CreateInMemory(dateTimeProvider);
        var now = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var claim = await SeedClaimCreatedAt(context, dateTimeProvider, now.AddHours(-100), ClaimStatus.Closed);
        dateTimeProvider.UtcNow = now;
        var sut = new SlaMonitoringJob(context, new TestAuditLogService(context, dateTimeProvider), dateTimeProvider);

        await sut.ExecuteAsync(CancellationToken.None);

        Assert.Empty(await context.ClaimAuditLogs.Where(a => a.ClaimId == claim.Id).ToListAsync());
    }

    /// <summary>§3.5: the claim itself is flagged SLA-breached, not just audited.</summary>
    [Fact]
    public async Task ExecuteAsync_StaleClaim_IsFlaggedSlaBreached()
    {
        var dateTimeProvider = new FakeDateTimeProvider();
        using var context = TestDbContext.CreateInMemory(dateTimeProvider);
        var now = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var claim = await SeedClaimCreatedAt(context, dateTimeProvider, now.AddHours(-49));
        dateTimeProvider.UtcNow = now;
        var sut = new SlaMonitoringJob(context, new TestAuditLogService(context, dateTimeProvider), dateTimeProvider);

        await sut.ExecuteAsync(CancellationToken.None);

        var flagged = await context.Claims.SingleAsync(c => c.Id == claim.Id);
        Assert.True(flagged.IsSlaBreached);
        Assert.Equal(now, flagged.SlaBreachedAt);
    }

    /// <summary>The recurring schedule must not re-flag or re-audit a claim that is still breached.</summary>
    [Fact]
    public async Task ExecuteAsync_RunAgainWhileStillBreached_DoesNotWriteAnotherEntry()
    {
        var dateTimeProvider = new FakeDateTimeProvider();
        using var context = TestDbContext.CreateInMemory(dateTimeProvider);
        var now = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var claim = await SeedClaimCreatedAt(context, dateTimeProvider, now.AddHours(-60));
        var auditLog = new TestAuditLogService(context, dateTimeProvider);
        var sut = new SlaMonitoringJob(context, auditLog, dateTimeProvider);

        dateTimeProvider.UtcNow = now;
        await sut.ExecuteAsync(CancellationToken.None);
        dateTimeProvider.UtcNow = now.AddMinutes(15);
        await sut.ExecuteAsync(CancellationToken.None);

        var entries = await context.ClaimAuditLogs.Where(a => a.ClaimId == claim.Id && a.Action == "SLA_BREACH_DETECTED").ToListAsync();
        Assert.Single(entries);
    }

    /// <summary>Activity clears the flag; if the claim then goes stale again it is a new breach.</summary>
    [Fact]
    public async Task ExecuteAsync_ClaimTouchedThenStaleAgain_IsFlaggedAgain()
    {
        var dateTimeProvider = new FakeDateTimeProvider();
        using var context = TestDbContext.CreateInMemory(dateTimeProvider);
        var now = new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        var claim = await SeedClaimCreatedAt(context, dateTimeProvider, now.AddHours(-60));
        var auditLog = new TestAuditLogService(context, dateTimeProvider);
        var sut = new SlaMonitoringJob(context, auditLog, dateTimeProvider);
        dateTimeProvider.UtcNow = now;
        await sut.ExecuteAsync(CancellationToken.None);

        dateTimeProvider.UtcNow = now.AddHours(1);
        claim.AddParty(PartyType.Individual, PartyRole.Witness, "Walter Witness", null, null, "tester");
        await context.SaveChangesAsync(CancellationToken.None);
        Assert.False(claim.IsSlaBreached);

        dateTimeProvider.UtcNow = now.AddHours(50);
        await sut.ExecuteAsync(CancellationToken.None);

        var entries = await context.ClaimAuditLogs.Where(a => a.ClaimId == claim.Id && a.Action == "SLA_BREACH_DETECTED").ToListAsync();
        Assert.Equal(2, entries.Count);
        Assert.True(claim.IsSlaBreached);
    }
}
