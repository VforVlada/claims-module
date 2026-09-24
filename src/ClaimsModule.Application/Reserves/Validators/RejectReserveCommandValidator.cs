using ClaimsModule.Application.Reserves.Commands;
using FluentValidation;

namespace ClaimsModule.Application.Reserves.Validators;

public sealed class RejectReserveCommandValidator : AbstractValidator<RejectReserveCommand>
{
    public RejectReserveCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.ReserveComponentId).NotEmpty();
        RuleFor(c => c.ReserveHistoryId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(1000);
    }
}
