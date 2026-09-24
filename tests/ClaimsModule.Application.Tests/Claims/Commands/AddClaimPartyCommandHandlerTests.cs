using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Application.Tests.Claims.Commands;

public class AddClaimPartyCommandHandlerTests
{
    private readonly FakeCurrentUserService _currentUser = new();

    private static Claim SeedClaim(TestDbContext context)
    {
        var claim = Claim.Create(Guid.NewGuid(), ClaimNumber.Create(2026, 1), null, ClaimType.Auto, "Hannah Handler", DateTimeOffset.UtcNow.AddDays(-1), "desc", "NY", Guid.NewGuid(), "tester");
        context.Claims.Add(claim);
        context.SaveChanges();
        return claim;
    }

    [Fact]
    public async Task Handle_ExistingClaim_AddsPartyAndPersists()
    {
        using var context = TestDbContext.Create();
        var claim = SeedClaim(context);
        var sut = new AddClaimPartyCommandHandler(context, _currentUser, TestMapperFactory.Create());

        var dto = await sut.Handle(new AddClaimPartyCommand(claim.Id, PartyType.Individual, PartyRole.Claimant, "John Doe", "john@example.com", null), CancellationToken.None);

        Assert.Equal("John Doe", dto.Name);
        var persisted = context.Claims.Single(c => c.Id == claim.Id);
        Assert.Single(persisted.Parties);
    }

    [Fact]
    public async Task Handle_UnknownClaim_ThrowsNotFoundException()
    {
        using var context = TestDbContext.Create();
        var sut = new AddClaimPartyCommandHandler(context, _currentUser, TestMapperFactory.Create());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            sut.Handle(new AddClaimPartyCommand(Guid.NewGuid(), PartyType.Individual, PartyRole.Claimant, "John Doe", null, null), CancellationToken.None));
    }
}
