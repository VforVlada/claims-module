using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Claims.Validators;
using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Tests.Claims.Validators;

public class CreateClaimCommandValidatorTests
{
    private readonly Guid _activeCauseOfLossCodeId = Guid.NewGuid();
    private readonly Guid _inactiveCauseOfLossCodeId = Guid.NewGuid();
    private readonly Guid _otherOrgCauseOfLossCodeId = Guid.NewGuid();
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly FakeCurrentUserService _currentUser = new();

    private TestDbContext SeedContext()
    {
        var context = TestDbContext.Create();
        context.CauseOfLossCodes.AddRange(
            new CauseOfLossCode { Id = _activeCauseOfLossCodeId, OrganizationEntityId = _currentUser.OrganizationEntityId, Code = "COLL", Description = "Collision", PerilCategory = "Auto", IsActive = true },
            new CauseOfLossCode { Id = _inactiveCauseOfLossCodeId, OrganizationEntityId = _currentUser.OrganizationEntityId, Code = "OLD", Description = "Retired code", PerilCategory = "Auto", IsActive = false },
            new CauseOfLossCode { Id = _otherOrgCauseOfLossCodeId, OrganizationEntityId = Guid.NewGuid(), Code = "OTHERORG", Description = "Belongs to a different org", PerilCategory = "Auto", IsActive = true });
        context.SaveChanges();
        return context;
    }

    private CreateClaimCommandValidator CreateValidator(TestDbContext context) => new(context, _currentUser, _dateTimeProvider);

    private CreateClaimCommand ValidCommand(InitialReserveInput? initialReserve = null) => new(
        PolicyId: null,
        ClaimType: ClaimType.Auto,
        LossDate: _dateTimeProvider.UtcNow.AddDays(-1),
        LossDescription: "Rear-end collision",
        LossLocation: "NY",
        CauseOfLossCodeId: _activeCauseOfLossCodeId,
        AssignedHandler: "Hannah Handler",
        Parties: [new ClaimPartyInput(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null)],
        RiskObjects: [],
        InitialReserve: initialReserve);

    [Fact]
    public async Task Validate_ValidCommand_HasNoErrors()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);

        var result = await sut.ValidateAsync(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_LossDateIsNowExactly_HasNoErrors()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { LossDate = _dateTimeProvider.UtcNow };

        var result = await sut.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_LossDateInFuture_FailsWithBRC01()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { LossDate = _dateTimeProvider.UtcNow.AddDays(1) };

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-C-01");
    }

    [Fact]
    public async Task Validate_InactiveCauseOfLossCode_FailsWithBRC05()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { CauseOfLossCodeId = _inactiveCauseOfLossCodeId };

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-C-05");
    }

    [Fact]
    public async Task Validate_UnknownCauseOfLossCode_FailsWithBRC05()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { CauseOfLossCodeId = Guid.NewGuid() };

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-C-05");
    }

    [Fact]
    public async Task Validate_CauseOfLossCodeBelongsToAnotherOrganization_FailsWithBRC05()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { CauseOfLossCodeId = _otherOrgCauseOfLossCodeId };

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-C-05");
    }

    [Fact]
    public async Task Validate_NoParties_FailsWithBRC03()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { Parties = [] };

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-C-03");
    }

    [Fact]
    public async Task Validate_OnlyAWitness_FailsWithBRC03()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with
        {
            Parties = [new ClaimPartyInput(PartyType.Individual, PartyRole.Witness, "Wendy Witness", null, null)]
        };

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-C-03");
    }

    [Fact]
    public async Task Validate_ClaimantPlusWitness_HasNoErrors()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with
        {
            Parties =
            [
                new ClaimPartyInput(PartyType.Individual, PartyRole.Claimant, "John Doe", null, null),
                new ClaimPartyInput(PartyType.Individual, PartyRole.Witness, "Wendy Witness", null, null)
            ]
        };

        var result = await sut.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Validate_MissingLossDescription_Fails(string description)
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { LossDescription = description };

        var result = await sut.ValidateAsync(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Validate_InitialReserveNonRecoveryWithZeroAmount_FailsWithBRR01()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand(new InitialReserveInput(ReserveComponentType.IndemnityReserve, 0m));

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-R-01");
    }

    [Fact]
    public async Task Validate_InitialReserveRecoveryWithNegativeAmount_Succeeds()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand(new InitialReserveInput(ReserveComponentType.RecoveryReserve, -5000m));

        var result = await sut.ValidateAsync(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Validate_InitialReserveRecoveryWithZeroAmount_FailsWithBRR01()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand(new InitialReserveInput(ReserveComponentType.RecoveryReserve, 0m));

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorCode == "BR-R-01");
    }

    [Fact]
    public async Task Validate_InitialReserveAboveMax_FailsWithMaxAmountMessage()
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand(new InitialReserveInput(ReserveComponentType.IndemnityReserve, ReserveLimits.MaxAmount + 1m));

        var result = await sut.ValidateAsync(command);

        Assert.Contains(result.Errors, e => e.ErrorMessage == ReserveLimits.MaxAmountMessage);
    }

    [Theory]
    [InlineData("+380501234567", true)]
    [InlineData("0501234567", true)]
    [InlineData(null, true)]
    [InlineData("050-123-4567", false)]
    [InlineData("+38 050 123", false)]
    [InlineData("38+050", false)]
    [InlineData("abc", false)]
    public async Task Validate_PartyContactPhone_AllowsOnlyDigitsAndLeadingPlus(string? phone, bool expectedValid)
    {
        using var context = SeedContext();
        var sut = CreateValidator(context);
        var command = ValidCommand() with { Parties = [new ClaimPartyInput(PartyType.Individual, PartyRole.Claimant, "John Doe", null, phone)] };

        var result = await sut.ValidateAsync(command);

        Assert.Equal(expectedValid, result.IsValid);
    }
}
