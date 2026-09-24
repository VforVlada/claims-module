using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.PersistenceTests;

/// <summary>
/// Plan section 3.2's 49-pair theory, asserted against the transition table the migrations
/// actually seeded (the unit-level ClaimStatusTransitionMatrixTests uses its own copy).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class SeededTransitionMatrixTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static readonly string[] AllRoles = ["Handler", "Supervisor", "Manager"];

    private static readonly Dictionary<(ClaimStatus From, ClaimStatus To), string[]> DocumentedGraph = new()
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

    public static IEnumerable<object[]> Roles() => AllRoles.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(Roles))]
    public async Task SeededTable_AllFortyNinePairs_MatchDocumentedGraph(string role)
    {
        using var scope = Factory.Services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IClaimStatusTransitionValidator>();
        var mismatches = new List<string>();

        foreach (var from in Enum.GetValues<ClaimStatus>())
        {
            foreach (var to in Enum.GetValues<ClaimStatus>())
            {
                var expected = !DocumentedGraph.TryGetValue((from, to), out var roles) ? TransitionCheck.NotInWorkflow
                    : roles.Contains(role) ? TransitionCheck.Allowed : TransitionCheck.RoleNotPermitted;
                var actual = await validator.CheckAsync(from, to, [role], CancellationToken.None);
                if (actual != expected)
                {
                    mismatches.Add($"{from} -> {to} as {role}: expected {expected}, seeded {actual}");
                }
            }
        }

        Assert.Empty(mismatches);
    }
}
