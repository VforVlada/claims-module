using ClaimsModule.Application.Common.Constants;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.Commands;
using ClaimsModule.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Reserves.Validators;

public sealed class AdjustReserveCommandValidator : AbstractValidator<AdjustReserveCommand>
{
    public AdjustReserveCommandValidator(IApplicationDbContext context)
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.ReserveComponentId).NotEmpty();
        RuleFor(c => c.Currency).NotEmpty().Matches(CurrencyRules.Pattern).WithMessage(CurrencyRules.Message);
        RuleFor(c => c.Amount).Must(a => Math.Abs(a) <= ReserveLimits.MaxAmount).WithMessage(ReserveLimits.MaxAmountMessage);
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(1000).WithMessage("A reason for the reserve change is required.");

        // BR-R-01 on the new amount. The sign depends on the component: recovery reserves are
        // carried negative (money expected back), everything else must be greater than zero.
        RuleFor(c => c.Amount)
            .MustAsync(async (command, amount, ct) =>
            {
                var type = await context.ClaimReserveComponents.AsNoTracking()
                    .Where(rc => rc.Id == command.ReserveComponentId && rc.ClaimId == command.ClaimId)
                    .Select(rc => (ReserveComponentType?)rc.ComponentType)
                    .FirstOrDefaultAsync(ct);
                return type switch
                {
                    null => true, // unknown component: the handler reports 404
                    ReserveComponentType.RecoveryReserve => amount < 0,
                    _ => amount > 0
                };
            })
            .WithErrorCode("BR-R-01")
            .WithMessage("The new reserve amount must be greater than zero (or negative for a recovery reserve).");
    }
}
