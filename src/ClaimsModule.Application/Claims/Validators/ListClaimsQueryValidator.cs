using ClaimsModule.Application.Claims.Queries;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Validators;

/// <summary>
/// Queries need no validator by convention, but this one takes user-built filters: an inverted
/// date range or an unknown status would otherwise just return an empty list, which reads as
/// "no claims" instead of "bad filter". Paging stays lenient (PagedList clamps it).
/// </summary>
public sealed class ListClaimsQueryValidator : AbstractValidator<ListClaimsQuery>
{
    public ListClaimsQueryValidator()
    {
        RuleForEach(q => q.Statuses).IsInEnum();

        RuleFor(q => q.ToDate)
            .GreaterThanOrEqualTo(q => q.FromDate!.Value)
            .When(q => q.FromDate is not null && q.ToDate is not null)
            .WithMessage("The 'to' date must be on or after the 'from' date.");

        RuleFor(q => q.AssignedHandler).MaximumLength(255);
    }
}
