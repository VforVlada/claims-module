using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Reserves.Commands;
using ClaimsModule.Domain.Enums;
using FluentValidation;

namespace ClaimsModule.Application.Reserves.Validators;

public sealed class OpenReserveCommandValidator : AbstractValidator<OpenReserveCommand>
{
    public OpenReserveCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.Currency).NotEmpty().Matches(CurrencyRules.Pattern).WithMessage(CurrencyRules.Message);
        RuleFor(c => c.Reason).MaximumLength(1000);
        RuleFor(c => c.Amount).Must(a => Math.Abs(a) <= ReserveLimits.MaxAmount).WithMessage(ReserveLimits.MaxAmountMessage);

        When(c => c.ComponentType != ReserveComponentType.RecoveryReserve, () =>
        {
            RuleFor(c => c.Amount).GreaterThan(0).WithErrorCode("BR-R-01").WithMessage("Reserve amount must be greater than zero.");
        }).Otherwise(() =>
        {
            RuleFor(c => c.Amount).LessThan(0).WithErrorCode("BR-R-01").WithMessage("A recovery reserve amount must be negative (money expected back).");
        });
    }
}
