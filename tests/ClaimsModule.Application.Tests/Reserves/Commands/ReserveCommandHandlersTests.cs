using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Services;
using ClaimsModule.Application.Reserves.Commands;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Exceptions;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Tests.Reserves.Commands;

public class OpenReserveCommandHandlerTests
{
    private readonly FakeCurrentUserService _currentUser = new();
    private readonly ReserveAuthorityEvaluator _authorityEvaluator = new();

    private static Claim SeedClaim(TestDbContext context)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        context.Claims.Add(claim);
        context.SaveChanges();
        return claim;
    }

    private OpenReserveCommandHandler CreateHandler(TestDbContext context) =>
        new(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

    [Fact]
    public async Task Handle_AmountWithinAutoThreshold_AutoApprovesReserve()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        var sut = CreateHandler(context);

        var result = await sut.Handle(new OpenReserveCommand(claim.Id, ReserveComponentType.IndemnityReserve, 5000m), CancellationToken.None);

        Assert.Equal(5000m, result.Value.CurrentAmount);
        Assert.Equal(ApprovalStatus.AutoApproved, result.Value.History.Single().ApprovalStatus);
    }

    [Fact]
    public async Task Handle_AmountAboveSupervisorThreshold_PendingApprovalDoesNotContributeToBalance()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        var sut = CreateHandler(context);

        var result = await sut.Handle(new OpenReserveCommand(claim.Id, ReserveComponentType.IndemnityReserve, 50000m), CancellationToken.None);

        Assert.Equal(0m, result.Value.CurrentAmount);
        Assert.Equal(ApprovalStatus.PendingApproval, result.Value.History.Single().ApprovalStatus);
    }

    [Fact]
    public async Task Handle_AmountExceedingAggregateCapWithoutOverride_ThrowsAggregateReserveCapExceededException()
    {
        // A single reserve above the Manager threshold starts PendingApproval and so does
        // not itself contribute to CurrentAmount (see the Auto-threshold test above) — the
        // aggregate cap can only be crossed by an amount that immediately contributes to the
        // balance, i.e. one within the Auto tier (<= $10K), on top of an already-large
        // approved balance. Simulate "the claim already has ~$9.99M approved from prior
        // activity" directly via the domain method (bypassing the authority evaluator, which
        // only gates new submissions, not this seed).
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(9_999_999m), ApprovalTier.Auto, "seed");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<AggregateReserveCapExceededException>(() =>
            sut.Handle(new OpenReserveCommand(claim.Id, ReserveComponentType.ExpenseReserve, 5000m), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AmountExceedingAggregateCapWithOverrideConfirmed_FlagsManagerOverrideWithBRR07Warning()
    {
        _currentUser.Roles = ["Manager"];
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(9_999_999m), ApprovalTier.Auto, "seed");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = CreateHandler(context);

        var result = await sut.Handle(new OpenReserveCommand(claim.Id, ReserveComponentType.ExpenseReserve, 5000m, ManagerOverrideConfirmed: true), CancellationToken.None);

        Assert.Contains(result.Warnings, w => w.Code == "BR-R-07");
        Assert.True(context.Claims.Single(c => c.Id == claim.Id).RequiresManagerOverride);
    }

    /// <summary>BR-R-07: only a Manager may override the cap — a Handler sending the flag directly is refused.</summary>
    [Fact]
    public async Task Handle_AmountExceedingAggregateCapWithOverrideFromNonManager_ThrowsForbidden()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(9_999_999m), ApprovalTier.Auto, "seed");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sut.Handle(new OpenReserveCommand(claim.Id, ReserveComponentType.ExpenseReserve, 5000m, ManagerOverrideConfirmed: true), CancellationToken.None));
        Assert.False(context.Claims.Single(c => c.Id == claim.Id).RequiresManagerOverride);
    }

    [Fact]
    public async Task Handle_AmountBringingAggregateToExactlyCap_AllowedWithoutOverride()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(9_999_999m), ApprovalTier.Auto, "seed");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = CreateHandler(context);

        var result = await sut.Handle(new OpenReserveCommand(claim.Id, ReserveComponentType.ExpenseReserve, 1m), CancellationToken.None);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "BR-R-07");
        Assert.False(context.Claims.Single(c => c.Id == claim.Id).RequiresManagerOverride);
    }

    [Fact]
    public async Task Handle_UnknownClaim_ThrowsNotFoundException()
    {
        using var context = TestDbContext.Create();
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.Handle(new OpenReserveCommand(Guid.NewGuid(), ReserveComponentType.IndemnityReserve, 5000m), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ClaimIsClosed_ThrowsInvalidReserveOperationException()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");
        claim.TransitionTo(ClaimStatus.Open, "tester");
        claim.TransitionTo(ClaimStatus.Closed, "tester");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<InvalidReserveOperationException>(() =>
            sut.Handle(new OpenReserveCommand(claim.Id, ReserveComponentType.IndemnityReserve, 5000m), CancellationToken.None));
    }
}

public class AdjustReserveCommandHandlerTests
{
    private readonly FakeCurrentUserService _currentUser = new();
    private readonly ReserveAuthorityEvaluator _authorityEvaluator = new();

    private static (Claim Claim, ClaimReserveComponent Component) SeedClaimWithReserve(TestDbContext context)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(5000m), ApprovalTier.Auto, "tester");
        context.Claims.Add(claim);
        context.SaveChanges();
        return (claim, component);
    }

    [Fact]
    public async Task Handle_AutoTierAdjustment_UpdatesBalanceImmediately()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedClaimWithReserve(context);
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        var result = await sut.Handle(new AdjustReserveCommand(claim.Id, component.Id, 6000m, "Test adjustment"), CancellationToken.None);

        Assert.Equal(6000m, result.Value.CurrentAmount);
        Assert.Equal(2, result.Value.History.Count);
    }

    /// <summary>
    /// U-R-16/U-R-17: adjusting 5,000 → 50,000 appends a history row (previous 5,000, new 50,000, change
    /// +45,000) with the next ChangeSequence, re-evaluates authority on the new amount, and parks it Supervisor-pending —
    /// the approved balance stays 5,000 until someone approves.
    /// </summary>
    [Fact]
    public async Task Handle_AdjustmentFrom5000To50000_AppendsSupervisorTierPendingChange()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedClaimWithReserve(context);
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        var result = await sut.Handle(new AdjustReserveCommand(claim.Id, component.Id, 50000m, "Test adjustment"), CancellationToken.None);

        var change = result.Value.History.Single(h => h.ChangeSequence == 2);
        Assert.Equal(2, result.Value.History.Count);
        Assert.Equal(ApprovalTier.Supervisor, change.RequiredTier);
        Assert.Equal(ApprovalStatus.PendingApproval, change.ApprovalStatus);
        Assert.Equal(45000m, change.Amount);
        Assert.Equal(5000m, change.PreviousAmount);
        Assert.Equal(50000m, change.NewAmount);
        Assert.Equal("Test adjustment", change.ChangeReason);
        Assert.Equal(5000m, result.Value.CurrentAmount);
        Assert.Equal(ApprovalStatus.AutoApproved, result.Value.History.Single(h => h.ChangeSequence == 1).ApprovalStatus);
    }

    [Fact]
    public async Task Handle_UnknownReserveComponent_ThrowsNotFoundException()
    {
        using var context = TestDbContext.Create();
        var (claim, _) = SeedClaimWithReserve(context);
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.Handle(new AdjustReserveCommand(claim.Id, Guid.NewGuid(), 1000m, "Test adjustment"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdjustmentExceedingAggregateCapWithoutOverride_ThrowsAggregateReserveCapExceededException()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedClaimWithReserve(context); // 5,000 Auto, already approved
        claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(9_994_995m), ApprovalTier.Auto, "seed"); // 5,000 + 9,994,995 = 9,999,995
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        // 5,000 → 5,010 (Auto tier on the new amount, contributes immediately) takes the total to 10,000,005.
        await Assert.ThrowsAsync<AggregateReserveCapExceededException>(() =>
            sut.Handle(new AdjustReserveCommand(claim.Id, component.Id, 5010m, "Test adjustment"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdjustmentExceedingAggregateCapWithOverrideConfirmed_SucceedsAndFlagsOverrideWithWarning()
    {
        _currentUser.Roles = ["Manager"];
        using var context = TestDbContext.Create();
        var (claim, component) = SeedClaimWithReserve(context);
        claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(9_994_995m), ApprovalTier.Auto, "seed");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        var result = await sut.Handle(new AdjustReserveCommand(claim.Id, component.Id, 5010m, "Test adjustment", ManagerOverrideConfirmed: true), CancellationToken.None);

        Assert.Contains(result.Warnings, w => w.Code == "BR-R-07");
        Assert.True(context.Claims.Single(c => c.Id == claim.Id).RequiresManagerOverride);
    }

    [Fact]
    public async Task Handle_AdjustmentExceedingAggregateCapWithOverrideFromNonManager_ThrowsForbidden()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedClaimWithReserve(context);
        claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(9_994_995m), ApprovalTier.Auto, "seed");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sut.Handle(new AdjustReserveCommand(claim.Id, component.Id, 5010m, "Test adjustment", ManagerOverrideConfirmed: true), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdjustmentBringingAggregateToExactlyCap_AllowedWithoutOverride()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedClaimWithReserve(context);
        claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(9_994_995m), ApprovalTier.Auto, "seed");
        await context.SaveChangesAsync(CancellationToken.None);
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        // 5,005 + 9,994,995 = 10,000,000 exactly — not "exceeding" the cap.
        var result = await sut.Handle(new AdjustReserveCommand(claim.Id, component.Id, 5005m, "Test adjustment"), CancellationToken.None);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "BR-R-07");
        Assert.False(context.Claims.Single(c => c.Id == claim.Id).RequiresManagerOverride);
    }

    [Fact]
    public async Task Handle_ReserveHasChangePendingApproval_ThrowsInvalidReserveOperationException()
    {
        using var context = TestDbContext.Create();
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(50000m), ApprovalTier.Supervisor, "tester");
        context.Claims.Add(claim);
        context.SaveChanges();
        var sut = new AdjustReserveCommandHandler(context, _authorityEvaluator, _currentUser, TestMapperFactory.Create());

        await Assert.ThrowsAsync<InvalidReserveOperationException>(() =>
            sut.Handle(new AdjustReserveCommand(claim.Id, component.Id, 1000m, "Test adjustment"), CancellationToken.None));
    }
}

public class ApproveReserveCommandHandlerTests
{
    private readonly ReserveAuthorityEvaluator _authorityEvaluator = new();
    private readonly FakeCurrentUserService _currentUser = new() { Roles = ["Supervisor"], UserName = "Sam Supervisor" };

    private static (Claim Claim, ClaimReserveComponent Component) SeedPendingReserve(TestDbContext context, ApprovalTier tier = ApprovalTier.Supervisor, string requestedBy = "tester")
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(50000m), tier, requestedBy);
        context.Claims.Add(claim);
        context.SaveChanges();
        return (claim, component);
    }

    private ApproveReserveCommandHandler CreateHandler(TestDbContext context) =>
        new(context, _authorityEvaluator, _currentUser, new FakeDateTimeProvider(), TestMapperFactory.Create());

    /// <summary>A claim already carrying 9,950,000 approved, plus a pending Manager-tier 150,000 change — approving it crosses the cap.</summary>
    private static (Claim Claim, ClaimReserveComponent Pending) SeedClaimNearCap(TestDbContext context)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var existing = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(9_950_000m), ApprovalTier.Manager, "tester");
        existing.Approve(existing.History.Single().Id, "earlier manager", DateTimeOffset.UtcNow);
        var pending = claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(150_000m), ApprovalTier.Manager, "tester");
        context.Claims.Add(claim);
        context.SaveChanges();
        return (claim, pending);
    }

    private static ApproveReserveCommandHandler ManagerHandler(TestDbContext context) =>
        new(context, new ReserveAuthorityEvaluator(), new FakeCurrentUserService { Roles = ["Manager"], UserName = "Mia Manager" }, new FakeDateTimeProvider(), TestMapperFactory.Create());

    /// <summary>BR-R-07 at approval: the cap is re-checked when a pending amount joins the balance.</summary>
    [Fact]
    public async Task Handle_ApprovalWouldExceedAggregateCap_WithoutOverride_ThrowsAggregateReserveCapExceededException()
    {
        using var context = TestDbContext.Create();
        var (claim, pending) = SeedClaimNearCap(context);

        await Assert.ThrowsAsync<AggregateReserveCapExceededException>(() =>
            ManagerHandler(context).Handle(new ApproveReserveCommand(claim.Id, pending.Id, pending.History.Single().Id), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ApprovalWouldExceedAggregateCap_ManagerOverride_ApprovesFlagsAndWarns()
    {
        using var context = TestDbContext.Create();
        var (claim, pending) = SeedClaimNearCap(context);

        var dto = await ManagerHandler(context).Handle(new ApproveReserveCommand(claim.Id, pending.Id, pending.History.Single().Id, ManagerOverrideConfirmed: true), CancellationToken.None);

        Assert.Equal(150_000m, dto.CurrentAmount);
        Assert.True(claim.RequiresManagerOverride);
        Assert.Contains(claim.DomainEvents, e => e is ClaimsModule.Domain.Events.ClaimWarningRaisedEvent { Code: "BR-R-07" });
    }

    /// <summary>A claim approved over the cap (10,100,000, flagged) with a pending Manager-tier decrease to 9,000,000.</summary>
    internal static (Claim Claim, ClaimReserveComponent Component, Guid PendingHistoryId) SeedFlaggedClaimWithPendingDecrease(TestDbContext context)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(10_100_000m), ApprovalTier.Manager, "tester");
        component.Approve(component.History.Single().Id, "earlier manager", DateTimeOffset.UtcNow);
        claim.UpdateAggregateCapFlag(true);
        var decrease = component.SubmitChange(new Money(9_000_000m), ApprovalTier.Manager, "tester", "Reserve reduced after review");
        context.Claims.Add(claim);
        context.SaveChanges();
        return (claim, component, decrease.Id);
    }

    /// <summary>BR-R-07: approving a decrease that brings the approved total back under the cap clears the flag.</summary>
    [Fact]
    public async Task Handle_ApprovingDecreaseBelowAggregateCap_ClearsManagerOverrideFlag()
    {
        using var context = TestDbContext.Create();
        var (claim, component, historyId) = SeedFlaggedClaimWithPendingDecrease(context);

        var dto = await ManagerHandler(context).Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None);

        Assert.Equal(9_000_000m, dto.CurrentAmount);
        Assert.False(context.Claims.Single(c => c.Id == claim.Id).RequiresManagerOverride);
    }

    [Fact]
    public async Task Handle_ApprovalWouldExceedAggregateCap_OverrideBySupervisor_ThrowsForbiddenAccessException()
    {
        using var context = TestDbContext.Create();
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var existing = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(9_950_000m), ApprovalTier.Manager, "tester");
        existing.Approve(existing.History.Single().Id, "earlier manager", DateTimeOffset.UtcNow);
        var pending = claim.OpenReserve(ReserveComponentType.ExpenseReserve, new Money(60_000m), ApprovalTier.Supervisor, "tester");
        context.Claims.Add(claim);
        context.SaveChanges();

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            CreateHandler(context).Handle(new ApproveReserveCommand(claim.Id, pending.Id, pending.History.Single().Id, ManagerOverrideConfirmed: true), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PendingReserve_ApprovesAndUpdatesBalance()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context);
        var historyId = component.History.Single().Id;
        var sut = CreateHandler(context);

        var dto = await sut.Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None);

        Assert.Equal(50000m, dto.CurrentAmount);
        Assert.Equal(ApprovalStatus.Approved, dto.History.Single().ApprovalStatus);
    }

    [Fact]
    public async Task Handle_AlreadyApprovedHistory_ThrowsInvalidReserveOperationException()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context);
        var historyId = component.History.Single().Id;
        var sut = CreateHandler(context);
        await sut.Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidReserveOperationException>(() =>
            sut.Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None));
    }

    // U-R-08: a Supervisor approving a Supervisor-tier reserve is exactly Handle_PendingReserve_ApprovesAndUpdatesBalance above.

    [Fact]
    public async Task Handle_SupervisorApprovingManagerTierReserve_ThrowsForbiddenAccessException()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context, ApprovalTier.Manager);
        var historyId = component.History.Single().Id;
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sut.Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ManagerApprovingSupervisorTierReserve_Approves()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context, ApprovalTier.Supervisor);
        var historyId = component.History.Single().Id;
        var manager = new FakeCurrentUserService { Roles = ["Manager"], UserName = "Mia Manager" };
        var sut = new ApproveReserveCommandHandler(context, _authorityEvaluator, manager, new FakeDateTimeProvider(), TestMapperFactory.Create());

        var dto = await sut.Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None);

        Assert.Equal(ApprovalStatus.Approved, dto.History.Single().ApprovalStatus);
    }

    [Fact]
    public async Task Handle_HandlerApprovingAnyReserve_ThrowsForbiddenAccessException()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context, ApprovalTier.Supervisor);
        var historyId = component.History.Single().Id;
        var handler = new FakeCurrentUserService { Roles = ["Handler"], UserName = "Hannah Handler" };
        var sut = new ApproveReserveCommandHandler(context, _authorityEvaluator, handler, new FakeDateTimeProvider(), TestMapperFactory.Create());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sut.Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ApproverIsRequester_ThrowsForbiddenAccessException()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context, ApprovalTier.Supervisor, requestedBy: "Sam Supervisor");
        var historyId = component.History.Single().Id;
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sut.Handle(new ApproveReserveCommand(claim.Id, component.Id, historyId), CancellationToken.None));
    }
}

public class RejectReserveCommandHandlerTests
{
    private readonly FakeCurrentUserService _currentUser = new() { Roles = ["Supervisor"], UserName = "Sam Supervisor" };

    private static (Claim Claim, ClaimReserveComponent Component) SeedPendingReserve(TestDbContext context, ApprovalTier tier = ApprovalTier.Supervisor)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        var component = claim.OpenReserve(ReserveComponentType.IndemnityReserve, new Money(tier == ApprovalTier.Manager ? 150000m : 50000m), tier, "tester");
        context.Claims.Add(claim);
        context.SaveChanges();
        return (claim, component);
    }

    /// <summary>RES-05 applies to rejection too: a Supervisor can't decide a Manager-tier change either way.</summary>
    [Fact]
    public async Task Handle_SupervisorRejectingManagerTierReserve_ThrowsForbiddenAccessException()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context, ApprovalTier.Manager);
        var historyId = component.History.Single().Id;
        var sut = new RejectReserveCommandHandler(context, new ReserveAuthorityEvaluator(), _currentUser, new FakeDateTimeProvider(), TestMapperFactory.Create());

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sut.Handle(new RejectReserveCommand(claim.Id, component.Id, historyId, "Too high"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_PendingReserve_RecordsDecisionTimeFromClock()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context);
        var historyId = component.History.Single().Id;
        var decidedAt = new DateTimeOffset(2026, 9, 23, 9, 30, 0, TimeSpan.Zero);
        var sut = new RejectReserveCommandHandler(context, new ReserveAuthorityEvaluator(), _currentUser, new FakeDateTimeProvider(decidedAt), TestMapperFactory.Create());

        var dto = await sut.Handle(new RejectReserveCommand(claim.Id, component.Id, historyId, "Not justified"), CancellationToken.None);

        Assert.Equal(decidedAt, dto.History.Single().DecidedAt);
    }

    [Fact]
    public async Task Handle_PendingReserve_RejectsAndRecordsReason()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context);
        var historyId = component.History.Single().Id;
        var sut = new RejectReserveCommandHandler(context, new ReserveAuthorityEvaluator(), _currentUser, new FakeDateTimeProvider(), TestMapperFactory.Create());

        var dto = await sut.Handle(new RejectReserveCommand(claim.Id, component.Id, historyId, "Not justified"), CancellationToken.None);

        Assert.Equal(0m, dto.CurrentAmount);
        Assert.Equal(ApprovalStatus.Rejected, dto.History.Single().ApprovalStatus);
        Assert.Equal("Not justified", dto.History.Single().RejectionReason);
    }

    /// <summary>BR-R-07: a rejected change never joined the approved total, so the cap flag is left as it was.</summary>
    [Fact]
    public async Task Handle_RejectingDecreaseOnClaimOverAggregateCap_LeavesManagerOverrideFlagSet()
    {
        using var context = TestDbContext.Create();
        var (claim, component, historyId) = ApproveReserveCommandHandlerTests.SeedFlaggedClaimWithPendingDecrease(context);
        var manager = new FakeCurrentUserService { Roles = ["Manager"], UserName = "Mia Manager" };
        var sut = new RejectReserveCommandHandler(context, new ReserveAuthorityEvaluator(), manager, new FakeDateTimeProvider(), TestMapperFactory.Create());

        var dto = await sut.Handle(new RejectReserveCommand(claim.Id, component.Id, historyId, "Keep the reserve as is"), CancellationToken.None);

        Assert.Equal(10_100_000m, dto.CurrentAmount);
        Assert.True(context.Claims.Single(c => c.Id == claim.Id).RequiresManagerOverride);
    }

    [Fact]
    public async Task Handle_ResubmissionAfterRejection_CreatesNewPendingHistoryEntry()
    {
        using var context = TestDbContext.Create();
        var (claim, component) = SeedPendingReserve(context);
        var historyId = component.History.Single().Id;
        var rejectHandler = new RejectReserveCommandHandler(context, new ReserveAuthorityEvaluator(), _currentUser, new FakeDateTimeProvider(), TestMapperFactory.Create());
        await rejectHandler.Handle(new RejectReserveCommand(claim.Id, component.Id, historyId, "Not justified"), CancellationToken.None);

        var authorityEvaluator = new ReserveAuthorityEvaluator();
        var adjustHandler = new AdjustReserveCommandHandler(context, authorityEvaluator, _currentUser, TestMapperFactory.Create());
        var result = await adjustHandler.Handle(new AdjustReserveCommand(claim.Id, component.Id, 45000m, "Test adjustment"), CancellationToken.None);

        Assert.Equal(2, result.Value.History.Count);
        Assert.Equal(ApprovalStatus.Rejected, result.Value.History.First(h => h.Id == historyId).ApprovalStatus);
    }
}
