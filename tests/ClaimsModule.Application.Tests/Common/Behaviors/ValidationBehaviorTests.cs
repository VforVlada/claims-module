using ClaimsModule.Application.Common.Behaviors;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Moq;
using ValidationException = ClaimsModule.Application.Common.Exceptions.ValidationException;

namespace ClaimsModule.Application.Tests.Common.Behaviors;

public class ValidationBehaviorTests
{
    public sealed record SampleRequest(string Name) : IRequest<string>;

    [Fact]
    public async Task Handle_NoValidators_CallsNextDirectly()
    {
        var sut = new ValidationBehavior<SampleRequest, string>([]);
        var nextCalled = false;

        var result = await sut.Handle(new SampleRequest(""), Next, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("ok", result);

        Task<string> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }
    }

    [Fact]
    public async Task Handle_ValidatorsPass_CallsNext()
    {
        var validator = new Mock<IValidator<SampleRequest>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SampleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var sut = new ValidationBehavior<SampleRequest, string>([validator.Object]);

        var result = await sut.Handle(new SampleRequest("valid"), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
    }

    [Fact]
    public async Task Handle_ValidatorFails_ThrowsValidationExceptionAndDoesNotCallNext()
    {
        var failure = new ValidationFailure("Name", "Name is required.");
        var validator = new Mock<IValidator<SampleRequest>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SampleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([failure]));

        var sut = new ValidationBehavior<SampleRequest, string>([validator.Object]);
        var nextCalled = false;

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            sut.Handle(new SampleRequest(""), Next, CancellationToken.None));

        Assert.False(nextCalled);
        Assert.Contains("Name", exception.Errors.Keys);

        Task<string> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult("ok");
        }
    }

    [Fact]
    public async Task Handle_MultipleValidatorsFail_AggregatesAllFailures()
    {
        var validatorA = new Mock<IValidator<SampleRequest>>();
        validatorA.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SampleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Name", "too short")]));

        var validatorB = new Mock<IValidator<SampleRequest>>();
        validatorB.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<SampleRequest>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Other", "invalid")]));

        var sut = new ValidationBehavior<SampleRequest, string>([validatorA.Object, validatorB.Object]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            sut.Handle(new SampleRequest(""), _ => Task.FromResult("ok"), CancellationToken.None));

        Assert.Equal(2, exception.Errors.Count);
    }
}
