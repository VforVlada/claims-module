using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Persistence.Seed;

/// <summary>
/// Assessment brief's simplified path: Draft -> Open -> UnderInvestigation -> PendingPayment ->
/// Closed, or Draft -> Open -> Closed directly for trivial claims (switchable, see
/// WorkflowSettings.AllowTrivialClaimClosure). Seeded per-role: any of Handler/Supervisor/Manager
/// can drive the workflow, except the pairs in RestrictedRoles below.
///
/// Reopened and Withdrawn aren't in the assessment brief's simplified path — the FRS reference
/// (Appendix A.1) only gives their one-line meaning ("previously closed claim re-activated",
/// "claimant withdrew the claim") and says the transition graph is the implementer's decision to
/// make and document. Rules applied here: Withdrawn is reachable from any pre-payment active
/// status (Draft/Open/UnderInvestigation) — once payment is in progress or the claim is settled,
/// "withdrawal" no longer applies, that's a different process. Reopened is reachable only from
/// Closed (per its FRS description), and from there rejoins the normal pipeline exactly where
/// Open does (UnderInvestigation, PendingPayment, or straight back to Closed for a trivial case).
/// </summary>
internal static class ClaimStatusTransitionSeed
{
    private static readonly DateTimeOffset SeededAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly string[] Roles = ["Handler", "Supervisor", "Manager"];

    // Original 8 pairs kept in their original order — sequence (and so each row's seeded Id) is
    // positional, so inserting a new pair anywhere but the end reassigns existing rows' Ids and
    // turns a clean migration into a pile of confusing UpdateData renames. New pairs go last.
    private static readonly (ClaimStatus From, ClaimStatus To)[] Pairs =
    [
        (ClaimStatus.Draft, ClaimStatus.Open),
        (ClaimStatus.Draft, ClaimStatus.Closed),
        (ClaimStatus.Open, ClaimStatus.UnderInvestigation),
        (ClaimStatus.Open, ClaimStatus.PendingPayment),
        (ClaimStatus.Open, ClaimStatus.Closed),
        (ClaimStatus.UnderInvestigation, ClaimStatus.PendingPayment),
        (ClaimStatus.UnderInvestigation, ClaimStatus.Closed),
        (ClaimStatus.PendingPayment, ClaimStatus.Closed),

        (ClaimStatus.Draft, ClaimStatus.Withdrawn),
        (ClaimStatus.Open, ClaimStatus.Withdrawn),
        (ClaimStatus.UnderInvestigation, ClaimStatus.Withdrawn),
        (ClaimStatus.Closed, ClaimStatus.Reopened),
        (ClaimStatus.Reopened, ClaimStatus.UnderInvestigation),
        (ClaimStatus.Reopened, ClaimStatus.PendingPayment),
        (ClaimStatus.Reopened, ClaimStatus.Closed)
    ];

    // Pairs later removed from the workflow. They stay in Pairs (and are skipped below) so every
    // other row keeps its positional Id. Draft -> Closed: a claim must be opened before it can be
    // closed (WF-02); the brief's trivial-claim shortcut is Open -> Closed, not Draft -> Closed.
    private static readonly HashSet<(ClaimStatus From, ClaimStatus To)> RetiredPairs =
    [
        (ClaimStatus.Draft, ClaimStatus.Closed)
    ];

    // Pairs restricted to fewer than all three roles (WF-05). Reopening a settled claim is a
    // supervisory decision, not something a handler does alone.
    private static readonly Dictionary<(ClaimStatus From, ClaimStatus To), string[]> RestrictedRoles = new()
    {
        [(ClaimStatus.Closed, ClaimStatus.Reopened)] = ["Supervisor", "Manager"]
    };

    public static readonly ClaimStatusTransition[] Transitions = BuildAll();

    private static ClaimStatusTransition[] BuildAll()
    {
        var rows = new List<ClaimStatusTransition>(Pairs.Length * Roles.Length);
        var sequence = 1;

        foreach (var (from, to) in Pairs)
        {
            foreach (var role in Roles)
            {
                var seedSequence = sequence++;
                if (RetiredPairs.Contains((from, to))
                    || (RestrictedRoles.TryGetValue((from, to), out var permitted) && !permitted.Contains(role)))
                {
                    continue;
                }

                rows.Add(new ClaimStatusTransition
                {
                    Id = Guid.Parse($"00000000-0000-0000-0000-0000000001{seedSequence:D2}"),
                    OrganizationEntityId = SeedIds.DefaultOrganization,
                    FromStatus = from,
                    ToStatus = to,
                    RequiredPermission = role,
                    CreatedAt = SeededAt,
                    UserCreated = "seed"
                });
            }
        }

        return [.. rows];
    }
}
