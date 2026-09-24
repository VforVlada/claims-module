using ClaimsModule.Application.Common.Models;
using ClaimsModule.Application.Common.Services;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Application.Tests.Common.Services;

public class ClaimStatusTransitionValidatorTests
{
    private static TestDbContext SeedContext()
    {
        var context = TestDbContext.Create();
        context.ClaimStatusTransitions.AddRange(
            new ClaimStatusTransition { Id = Guid.NewGuid(), FromStatus = ClaimStatus.Draft, ToStatus = ClaimStatus.Open, RequiredPermission = "Handler" },
            new ClaimStatusTransition { Id = Guid.NewGuid(), FromStatus = ClaimStatus.Open, ToStatus = ClaimStatus.UnderInvestigation, RequiredPermission = "Handler" },
            new ClaimStatusTransition { Id = Guid.NewGuid(), FromStatus = ClaimStatus.Open, ToStatus = ClaimStatus.Closed, RequiredPermission = "Supervisor" },
            new ClaimStatusTransition { Id = Guid.NewGuid(), FromStatus = ClaimStatus.PendingPayment, ToStatus = ClaimStatus.Closed, RequiredPermission = "Manager" });
        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task IsAllowedAsync_MatchingTransitionAndRole_ReturnsTrue()
    {
        using var context = SeedContext();
        var sut = new ClaimStatusTransitionValidator(context, Options.Create(new WorkflowSettings()));

        var allowed = await sut.IsAllowedAsync(ClaimStatus.Draft, ClaimStatus.Open, ["Handler"], CancellationToken.None);

        Assert.True(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_MatchingTransitionWrongRole_ReturnsFalse()
    {
        using var context = SeedContext();
        var sut = new ClaimStatusTransitionValidator(context, Options.Create(new WorkflowSettings()));

        var allowed = await sut.IsAllowedAsync(ClaimStatus.Open, ClaimStatus.Closed, ["Handler"], CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public async Task IsAllowedAsync_NoSeededTransition_ReturnsFalse()
    {
        using var context = SeedContext();
        var sut = new ClaimStatusTransitionValidator(context, Options.Create(new WorkflowSettings()));

        var allowed = await sut.IsAllowedAsync(ClaimStatus.Closed, ClaimStatus.Draft, ["Manager"], CancellationToken.None);

        Assert.False(allowed);
    }

    [Fact]
    public async Task GetAllowedNextStatusesAsync_ReturnsOnlyStatusesReachableByUsersRoles()
    {
        using var context = SeedContext();
        var sut = new ClaimStatusTransitionValidator(context, Options.Create(new WorkflowSettings()));

        var next = await sut.GetAllowedNextStatusesAsync(ClaimStatus.Open, ["Handler"], CancellationToken.None);

        Assert.Equal([ClaimStatus.UnderInvestigation], next);
    }

    [Fact]
    public async Task GetAllowedNextStatusesAsync_WithMultipleRoles_UnionsReachableStatuses()
    {
        using var context = SeedContext();
        var sut = new ClaimStatusTransitionValidator(context, Options.Create(new WorkflowSettings()));

        var next = await sut.GetAllowedNextStatusesAsync(ClaimStatus.Open, ["Handler", "Supervisor"], CancellationToken.None);

        Assert.Equal(2, next.Count);
        Assert.Contains(ClaimStatus.UnderInvestigation, next);
        Assert.Contains(ClaimStatus.Closed, next);
    }
}

/// <summary>
/// U-W-01..05: the full 7x7 = 49 (from, to) pair matrix across all seven statuses, for every
/// role, seeded with the same graph as ClaimsModule.Persistence.Seed.ClaimStatusTransitionSeed
/// (kept independent here — this asserts the validator's logic against a known table;
/// SeededTransitionMatrixTests in the integration project asserts the real seeded table).
/// </summary>
public class ClaimStatusTransitionMatrixTests
{
    private static readonly string[] AllRoles = ["Handler", "Supervisor", "Manager"];

    /// <summary>The documented graph: (from, to) → roles permitted to perform it.</summary>
    public static readonly Dictionary<(ClaimStatus From, ClaimStatus To), string[]> Graph = new()
    {
        [(ClaimStatus.Draft, ClaimStatus.Open)] = AllRoles,
        [(ClaimStatus.Draft, ClaimStatus.Withdrawn)] = AllRoles,
        [(ClaimStatus.Open, ClaimStatus.UnderInvestigation)] = AllRoles,
        [(ClaimStatus.Open, ClaimStatus.PendingPayment)] = AllRoles,
        [(ClaimStatus.Open, ClaimStatus.Closed)] = AllRoles,
        [(ClaimStatus.Open, ClaimStatus.Withdrawn)] = AllRoles,
        [(ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment)] = AllRoles,
        [(ClaimStatus.UnderInvestigation, ClaimStatus.Closed)] = AllRoles,
        [(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn)] = AllRoles,
        [(ClaimStatus.PendingPayment, ClaimStatus.Closed)] = AllRoles,
        [(ClaimStatus.Closed, ClaimStatus.Reopened)] = ["Supervisor", "Manager"],
        [(ClaimStatus.Reopened, ClaimStatus.UnderInvestigation)] = AllRoles,
        [(ClaimStatus.Reopened, ClaimStatus.PendingPayment)] = AllRoles,
        [(ClaimStatus.Reopened, ClaimStatus.Closed)] = AllRoles
    };

    public static IEnumerable<object[]> AllPairsForEveryRole()
    {
        var statuses = Enum.GetValues<ClaimStatus>();
        foreach (var role in AllRoles)
        {
            foreach (var from in statuses)
            {
                foreach (var to in statuses)
                {
                    yield return [role, from, to, Expected(from, to, role)];
                }
            }
        }
    }

    public static TransitionCheck Expected(ClaimStatus from, ClaimStatus to, string role) =>
        !Graph.TryGetValue((from, to), out var roles) ? TransitionCheck.NotInWorkflow
        : roles.Contains(role) ? TransitionCheck.Allowed
        : TransitionCheck.RoleNotPermitted;

    private static TestDbContext SeedTransitionTable()
    {
        var context = TestDbContext.Create();
        foreach (var ((from, to), roles) in Graph)
        {
            foreach (var role in roles)
            {
                context.ClaimStatusTransitions.Add(new ClaimStatusTransition
                {
                    Id = Guid.NewGuid(),
                    FromStatus = from,
                    ToStatus = to,
                    RequiredPermission = role
                });
            }
        }
        context.SaveChanges();
        return context;
    }

    private static ClaimStatusTransitionValidator CreateValidator(TestDbContext context, bool allowTrivialClaimClosure = true) =>
        new(context, Options.Create(new WorkflowSettings { AllowTrivialClaimClosure = allowTrivialClaimClosure }));

    [Theory]
    [MemberData(nameof(AllPairsForEveryRole))]
    public async Task CheckAsync_AllFortyNinePairs_MatchesDocumentedTransitionGraph(string role, ClaimStatus from, ClaimStatus to, TransitionCheck expected)
    {
        using var context = SeedTransitionTable();
        var sut = CreateValidator(context);

        var result = await sut.CheckAsync(from, to, [role], CancellationToken.None);

        Assert.Equal(expected, result);
    }

    /// <summary>U-W-01: Draft → Closed is rejected — a claim must be opened first.</summary>
    [Fact]
    public async Task CheckAsync_DraftToClosed_NotInWorkflow()
    {
        using var context = SeedTransitionTable();

        var result = await CreateValidator(context).CheckAsync(ClaimStatus.Draft, ClaimStatus.Closed, AllRoles, CancellationToken.None);

        Assert.Equal(TransitionCheck.NotInWorkflow, result);
    }

    /// <summary>U-W-02: the full happy path is legal step by step.</summary>
    [Fact]
    public async Task CheckAsync_HappyPathDraftToClosed_EachStepAllowed()
    {
        using var context = SeedTransitionTable();
        var sut = CreateValidator(context);
        ClaimStatus[] path = [ClaimStatus.Draft, ClaimStatus.Open, ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment, ClaimStatus.Closed];

        foreach (var (from, to) in path.Zip(path.Skip(1)))
        {
            Assert.Equal(TransitionCheck.Allowed, await sut.CheckAsync(from, to, ["Handler"], CancellationToken.None));
        }
    }

    /// <summary>U-W-03: Open → Closed (the trivial-claim shortcut) follows the config switch; nothing else changes.</summary>
    [Theory]
    [InlineData(true, TransitionCheck.Allowed)]
    [InlineData(false, TransitionCheck.NotInWorkflow)]
    public async Task CheckAsync_OpenToClosed_FollowsTrivialClaimClosureSetting(bool enabled, TransitionCheck expected)
    {
        using var context = SeedTransitionTable();
        var sut = CreateValidator(context, enabled);

        Assert.Equal(expected, await sut.CheckAsync(ClaimStatus.Open, ClaimStatus.Closed, ["Handler"], CancellationToken.None));
        Assert.Equal(TransitionCheck.Allowed, await sut.CheckAsync(ClaimStatus.UnderInvestigation, ClaimStatus.Closed, ["Handler"], CancellationToken.None));
    }

    [Fact]
    public async Task GetAllowedNextStatusesAsync_TrivialClaimClosureOff_OmitsClosedFromOpen()
    {
        using var context = SeedTransitionTable();

        var next = await CreateValidator(context, allowTrivialClaimClosure: false)
            .GetAllowedNextStatusesAsync(ClaimStatus.Open, ["Handler"], CancellationToken.None);

        Assert.DoesNotContain(ClaimStatus.Closed, next);
        Assert.Contains(ClaimStatus.UnderInvestigation, next);
    }

    /// <summary>U-W-05: Withdrawn only before payment starts; Reopened only from Closed, then rejoins the pipeline.</summary>
    [Theory]
    [InlineData(ClaimStatus.Draft, ClaimStatus.Withdrawn, TransitionCheck.Allowed)]
    [InlineData(ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn, TransitionCheck.Allowed)]
    [InlineData(ClaimStatus.PendingPayment, ClaimStatus.Withdrawn, TransitionCheck.NotInWorkflow)]
    [InlineData(ClaimStatus.Withdrawn, ClaimStatus.Open, TransitionCheck.NotInWorkflow)]
    [InlineData(ClaimStatus.Closed, ClaimStatus.Reopened, TransitionCheck.Allowed)]
    [InlineData(ClaimStatus.Open, ClaimStatus.Reopened, TransitionCheck.NotInWorkflow)]
    [InlineData(ClaimStatus.Reopened, ClaimStatus.UnderInvestigation, TransitionCheck.Allowed)]
    public async Task CheckAsync_ReopenedAndWithdrawnPaths_FollowDocumentedRules(ClaimStatus from, ClaimStatus to, TransitionCheck expected)
    {
        using var context = SeedTransitionTable();

        Assert.Equal(expected, await CreateValidator(context).CheckAsync(from, to, ["Supervisor"], CancellationToken.None));
    }
}
