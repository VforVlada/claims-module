namespace ClaimsModule.Application.Common.Models;

/// <summary>
/// Wraps a successful handler result plus any non-blocking warnings (e.g. BR-C-02:
/// loss date outside the policy period is recorded but does not stop claim creation).
/// Blocking validation failures are raised as exceptions via ValidationBehavior instead.
/// </summary>
public sealed class Result<T>
{
    public T Value { get; }

    public IReadOnlyCollection<ValidationIssue> Warnings { get; }

    private Result(T value, IReadOnlyCollection<ValidationIssue> warnings)
    {
        Value = value;
        Warnings = warnings;
    }

    public static Result<T> Success(T value) => new(value, []);

    public static Result<T> Success(T value, IEnumerable<ValidationIssue> warnings) => new(value, warnings.ToList());

    public static implicit operator Result<T>(T value) => Success(value);
}
