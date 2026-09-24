using ClaimsModule.Application.Claims.Commands;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Validators;

public sealed class TransitionClaimStatusCommandValidator : AbstractValidator<TransitionClaimStatusCommand>
{
    public TransitionClaimStatusCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.NewStatus).IsInEnum();
    }
}
