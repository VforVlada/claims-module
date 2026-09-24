using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Validators;

public sealed class CreateClaimCommandValidator : AbstractValidator<CreateClaimCommand>
{
    public CreateClaimCommandValidator(IApplicationDbContext context, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    {
        RuleFor(c => c.LossDate)
            .LessThanOrEqualTo(_ => dateTimeProvider.UtcNow)
            .WithErrorCode("BR-C-01")
            .WithMessage("Loss date cannot be in the future.");

        RuleFor(c => c.LossDescription).NotEmpty().MaximumLength(2000);
        RuleFor(c => c.LossLocation).NotEmpty().MaximumLength(500);
        RuleFor(c => c.AssignedHandler).NotEmpty().MaximumLength(255);

        // Also enforced by ClaimsDbContext's global tenant query filter in production — checked
        // explicitly here too so this rule (BR-C-05 / FNOL-05) is provable at the Application
        // layer without depending on that Persistence-layer detail.
        RuleFor(c => c.CauseOfLossCodeId)
            .MustAsync(async (id, ct) => await context.CauseOfLossCodes
                .AnyAsync(c => c.Id == id && c.IsActive && c.OrganizationEntityId == currentUser.OrganizationEntityId, ct))
            .WithErrorCode("BR-C-05")
            .WithMessage("Cause of loss code must exist, be active, and belong to your organization.");

        RuleFor(c => c.Parties)
            .NotEmpty()
            .WithErrorCode("BR-C-03")
            .WithMessage("At least one party is required.");

        RuleFor(c => c.Parties)
            .Must(parties => parties.Any(p => p.PartyRole == PartyRole.Claimant))
            .WithErrorCode("BR-C-03")
            .WithMessage("At least one Claimant party is required.")
            .When(c => c.Parties.Count > 0);

        RuleForEach(c => c.Parties).ChildRules(party =>
        {
            party.RuleFor(p => p.Name).NotEmpty().MaximumLength(255);
            party.RuleFor(p => p.ContactEmail).EmailAddress().MaximumLength(255).When(p => !string.IsNullOrWhiteSpace(p.ContactEmail));
            party.RuleFor(p => p.ContactPhone)
                .Matches(ContactRules.PhonePattern).WithMessage(ContactRules.PhoneMessage)
                .MaximumLength(ContactRules.PhoneMaxLength)
                .When(p => !string.IsNullOrWhiteSpace(p.ContactPhone));
        });

        RuleForEach(c => c.RiskObjects).ChildRules(riskObject =>
        {
            riskObject.RuleFor(r => r.Description).NotEmpty().MaximumLength(1000);
            riskObject.RuleFor(r => r.Identifier).MaximumLength(255);
        });

        RuleFor(c => c.InitialReserve!.Amount)
            .Must(a => Math.Abs(a) <= ReserveLimits.MaxAmount)
            .WithMessage(ReserveLimits.MaxAmountMessage)
            .When(c => c.InitialReserve is not null);

        RuleFor(c => c.InitialReserve!.Currency)
            .NotEmpty()
            .Matches(CurrencyRules.Pattern).WithMessage(CurrencyRules.Message)
            .When(c => c.InitialReserve is not null);

        When(c => c.InitialReserve is not null && c.InitialReserve.ComponentType != ReserveComponentType.RecoveryReserve, () =>
        {
            RuleFor(c => c.InitialReserve!.Amount)
                .GreaterThan(0)
                .WithErrorCode("BR-R-01")
                .WithMessage("Reserve amount must be greater than zero.");
        }).Otherwise(() =>
        {
            When(c => c.InitialReserve is not null, () =>
            {
                RuleFor(c => c.InitialReserve!.Amount)
                    .LessThan(0)
                    .WithErrorCode("BR-R-01")
                    .WithMessage("A recovery reserve amount must be negative (money expected back).");
            });
        });
    }
}
