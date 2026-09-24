using ClaimsModule.Application.Policies.Queries;
using FluentValidation;

namespace ClaimsModule.Application.Policies.Validators;

/// <summary>A blank term would match every policy; the FNOL typeahead only searches from 2 characters.</summary>
public sealed class SearchPoliciesQueryValidator : AbstractValidator<SearchPoliciesQuery>
{
    public const int MinimumTermLength = 2;

    public SearchPoliciesQueryValidator()
    {
        RuleFor(q => q.SearchTerm)
            .Must(t => t is not null && t.Trim().Length >= MinimumTermLength)
            .WithMessage($"Enter at least {MinimumTermLength} characters of a policy number or client name.")
            .MaximumLength(100);
    }
}
