using ClaimsModule.Application.Claims.Queries;
using ClaimsModule.Application.Claims.Validators;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Tests.Claims.Validators;

public class ListClaimsQueryValidatorTests
{
    private static readonly DateTimeOffset Day = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ListClaimsQueryValidator _sut = new();

    [Fact]
    public void NoFilters_IsValid() =>
        Assert.True(_sut.Validate(new ListClaimsQuery(null, null, null, null, null)).IsValid);

    [Fact]
    public void SameDayRange_IsValid() =>
        Assert.True(_sut.Validate(new ListClaimsQuery(null, Day, Day, null, null)).IsValid);

    [Fact]
    public void ToDateBeforeFromDate_IsInvalid()
    {
        var result = _sut.Validate(new ListClaimsQuery(null, Day, Day.AddDays(-1), null, null));

        var error = Assert.Single(result.Errors);
        Assert.Equal(nameof(ListClaimsQuery.ToDate), error.PropertyName);
    }

    [Fact]
    public void UnknownStatus_IsInvalid() =>
        Assert.False(_sut.Validate(new ListClaimsQuery([(ClaimStatus)99], null, null, null, null)).IsValid);
}
