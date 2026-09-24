using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Common.Constants;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Validators;

public sealed class AddClaimPartyCommandValidator : AbstractValidator<AddClaimPartyCommand>
{
    public AddClaimPartyCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(255);
        RuleFor(c => c.ContactPhone)
            .Matches(ContactRules.PhonePattern).WithMessage(ContactRules.PhoneMessage)
            .MaximumLength(ContactRules.PhoneMaxLength)
            .When(c => !string.IsNullOrWhiteSpace(c.ContactPhone));
        RuleFor(c => c.ContactEmail).EmailAddress().MaximumLength(255).When(c => !string.IsNullOrWhiteSpace(c.ContactEmail));
    }
}
