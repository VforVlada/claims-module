using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Services;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using ClaimsModule.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Application.Tests.Claims.Commands;

public class TransitionClaimStatusCommandHandlerTests
{
    private readonly FakeCurrentUserService _currentUser = new() { Roles = ["Handler"] };

    private static Claim SeedClaim(TestDbContext context, bool withClaimant = true)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        if (withClaimant)
        {
            claim.AddParty(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null, "tester");
        }

        context.Claims.Add(claim);
        context.SaveChanges();
        return claim;
    }

    private TransitionClaimStatusCommandHandler CreateHandler(TestDbContext context)
    {
        context.ClaimStatusTransitions.Add(new ClaimStatusTransition { Id = Guid.NewGuid(), FromStatus = ClaimStatus.Draft, ToStatus = ClaimStatus.Open, RequiredPermission = "Handler" });
        context.SaveChanges();

        return new TransitionClaimStatusCommandHandler(context, new ClaimStatusTransitionValidator(context, Options.Create(new WorkflowSettings())), _currentUser, TestMapperFactory.Create());
    }

    [Fact]
    public async Task Handle_AllowedTransition_UpdatesStatus()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        var sut = CreateHandler(context);

        var result = await sut.Handle(new TransitionClaimStatusCommand(claim.Id, ClaimStatus.Open), CancellationToken.None);

        Assert.Equal(ClaimStatus.Open, result.Status);
    }

    /// <summary>WF-05: the pair exists in the workflow, but not for the caller's role → 403, not 409.</summary>
    [Fact]
    public async Task Handle_TransitionExistsButNotForRole_ThrowsForbiddenAccessException()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        var sut = CreateHandler(context);
        _currentUser.Roles = ["Supervisor"];

        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            sut.Handle(new TransitionClaimStatusCommand(claim.Id, ClaimStatus.Open), CancellationToken.None));
    }

    /// <summary>U-W-01 / WF-02: a pair not in the workflow for anyone → InvalidClaimStatusTransitionException listing the legal next statuses.</summary>
    [Fact]
    public async Task Handle_PairNotInWorkflow_ThrowsInvalidClaimStatusTransitionExceptionWithAllowedNextStatuses()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        var sut = CreateHandler(context);

        var exception = await Assert.ThrowsAsync<InvalidClaimStatusTransitionException>(() =>
            sut.Handle(new TransitionClaimStatusCommand(claim.Id, ClaimStatus.Closed), CancellationToken.None));

        Assert.Equal([ClaimStatus.Open], exception.AllowedNextStatuses);
    }

    /// <summary>U-W-04: transitioning to the current status is rejected, never a silent no-op.</summary>
    [Fact]
    public async Task Handle_TransitionToSameStatus_ThrowsInvalidClaimStatusTransitionException()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<InvalidClaimStatusTransitionException>(() =>
            sut.Handle(new TransitionClaimStatusCommand(claim.Id, ClaimStatus.Draft), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownClaim_ThrowsNotFoundException()
    {
        using var context = TestDbContext.Create();
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.Handle(new TransitionClaimStatusCommand(Guid.NewGuid(), ClaimStatus.Open), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_LeavingDraftWithoutClaimant_ThrowsDomainInvariantException()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context, withClaimant: false);
        var sut = CreateHandler(context);

        await Assert.ThrowsAsync<ClaimsModule.Domain.Exceptions.ClaimInvariantViolationException>(() =>
            sut.Handle(new TransitionClaimStatusCommand(claim.Id, ClaimStatus.Open), CancellationToken.None));
    }
}
