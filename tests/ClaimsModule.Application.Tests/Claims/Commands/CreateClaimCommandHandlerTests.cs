using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Services;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.ValueObjects;
using Moq;

namespace ClaimsModule.Application.Tests.Claims.Commands;

public class CreateClaimCommandHandlerTests
{
    private readonly FakeCurrentUserService _currentUser = new();
    private readonly Mock<IClaimNumberGenerator> _claimNumberGenerator = new();
    private readonly ReserveAuthorityEvaluator _authorityEvaluator = new();

    public CreateClaimCommandHandlerTests()
    {
        _claimNumberGenerator
            .Setup(g => g.NextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ClaimNumber.Create(2026, 1));
    }

    private CreateClaimCommandHandler CreateHandler(TestDbContext context) => new(
        context,
        _claimNumberGenerator.Object,
        _authorityEvaluator,
        _currentUser,
        TestMapperFactory.Create());

    private static CreateClaimCommand ValidCommand(Guid? policyId = null, DateTimeOffset? lossDate = null, InitialReserveInput? initialReserve = null) => new(
        PolicyId: policyId,
        ClaimType: ClaimType.Auto,
        LossDate: lossDate ?? DateTimeOffset.UtcNow.AddDays(-1),
        LossDescription: "Rear-end collision",
        LossLocation: "NY",
        CauseOfLossCodeId: Guid.NewGuid(),
        AssignedHandler: "Hannah Handler",
        Parties: [new ClaimPartyInput(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null)],
        RiskObjects: [],
        InitialReserve: initialReserve);

    [Fact]
    public async Task Handle_ValidCommand_CreatesClaimInDraftStatusWithGeneratedClaimNumber()
    {
        using var context = TestDbContext.Create();
        var sut = CreateHandler(context);

        var result = await sut.Handle(ValidCommand(), CancellationToken.None);

        Assert.Equal("CLM-2026-0000001", result.Value.ClaimNumber);
        Assert.Equal(ClaimStatus.Draft, result.Value.Status);
        Assert.Single(context.Claims);
        _claimNumberGenerator.Verify(g => g.NextAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PolicyIdSetButLossDateOutsidePolicyPeriod_AddsBRC02Warning()
    {
        using var context = TestDbContext.Create();
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-1",
            ClientName = "Acme",
            EffectiveDate = DateTimeOffset.UtcNow.AddYears(-1),
            ExpirationDate = DateTimeOffset.UtcNow.AddMonths(-6)
        };
        context.Policies.Add(policy);
        await context.SaveChangesAsync(CancellationToken.None);

        var sut = CreateHandler(context);
        var command = ValidCommand(policyId: policy.Id, lossDate: DateTimeOffset.UtcNow.AddDays(-1));

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.Contains(result.Warnings, w => w.Code == "BR-C-02");
        // ...and is raised as a domain event, so it reaches the audit trail post-commit (I-API-02).
        Assert.Contains(context.GetEntitiesWithDomainEvents().SelectMany(e => e.DomainEvents), e => e is ClaimWarningRaisedEvent { Code: "BR-C-02" });
    }

    [Fact]
    public async Task Handle_PolicyIdSetAndLossDateWithinPolicyPeriod_NoWarning()
    {
        using var context = TestDbContext.Create();
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-1",
            ClientName = "Acme",
            EffectiveDate = DateTimeOffset.UtcNow.AddYears(-1),
            ExpirationDate = DateTimeOffset.UtcNow.AddYears(1)
        };
        context.Policies.Add(policy);
        await context.SaveChangesAsync(CancellationToken.None);

        var sut = CreateHandler(context);
        var command = ValidCommand(policyId: policy.Id, lossDate: DateTimeOffset.UtcNow.AddDays(-1));

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "BR-C-02");
    }

    [Theory]
    [MemberData(nameof(PolicyBoundaryDates))]
    public async Task Handle_LossDateOnPolicyEffectiveOrExpirationBoundary_NoWarning(Func<Policy, DateTimeOffset> pickBoundary)
    {
        using var context = TestDbContext.Create();
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            PolicyNumber = "POL-1",
            ClientName = "Acme",
            EffectiveDate = DateTimeOffset.UtcNow.AddYears(-1),
            ExpirationDate = DateTimeOffset.UtcNow.AddDays(-1)
        };
        context.Policies.Add(policy);
        await context.SaveChangesAsync(CancellationToken.None);

        var sut = CreateHandler(context);
        var command = ValidCommand(policyId: policy.Id, lossDate: pickBoundary(policy));

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.DoesNotContain(result.Warnings, w => w.Code == "BR-C-02");
    }

    public static IEnumerable<object[]> PolicyBoundaryDates()
    {
        yield return [(Func<Policy, DateTimeOffset>)(p => p.EffectiveDate)];
        yield return [(Func<Policy, DateTimeOffset>)(p => p.ExpirationDate)];
    }

    [Fact]
    public async Task Handle_InitialReserveAboveManagerThreshold_RoutesToManagerTierPendingApproval()
    {
        // A brand-new claim's first-ever reserve can never itself trip the BR-R-07 aggregate
        // cap: an amount above the Manager threshold starts PendingApproval, which does not
        // contribute to CurrentAmount, so the aggregate total the handler checks stays at
        // zero regardless of how large the requested amount is. What IS observable here is
        // that it correctly routes to the Manager approval tier instead of auto-approving.
        using var context = TestDbContext.Create();
        var sut = CreateHandler(context);
        var command = ValidCommand(initialReserve: new InitialReserveInput(ReserveComponentType.IndemnityReserve, 10_000_001m));

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.False(result.Value.RequiresManagerOverride);
        Assert.DoesNotContain(result.Warnings, w => w.Code == "BR-R-07");
        Assert.Equal(0m, result.Value.ReserveComponents.Single().CurrentAmount);
        Assert.Equal(ApprovalStatus.PendingApproval, result.Value.ReserveComponents.Single().History.Single().ApprovalStatus);
    }

    [Fact]
    public async Task Handle_InitialReserveWithinCap_DoesNotFlagManagerOverride()
    {
        using var context = TestDbContext.Create();
        var sut = CreateHandler(context);
        var command = ValidCommand(initialReserve: new InitialReserveInput(ReserveComponentType.IndemnityReserve, 5000m));

        var result = await sut.Handle(command, CancellationToken.None);

        Assert.False(result.Value.RequiresManagerOverride);
        Assert.Single(result.Value.ReserveComponents);
    }

    [Fact]
    public async Task Handle_NoInitialReserve_CreatesClaimWithNoReserveComponents()
    {
        using var context = TestDbContext.Create();
        var sut = CreateHandler(context);

        var result = await sut.Handle(ValidCommand(), CancellationToken.None);

        Assert.Empty(result.Value.ReserveComponents);
    }
}
