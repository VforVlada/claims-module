using ClaimsModule.Application.Reserves.Commands;
using FluentValidation;

namespace ClaimsModule.Application.Reserves.Validators;

public sealed class ApproveReserveCommandValidator : AbstractValidator<ApproveReserveCommand>
{
    public ApproveReserveCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.ReserveComponentId).NotEmpty();
        RuleFor(c => c.ReserveHistoryId).NotEmpty();
    }
}
