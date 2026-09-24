using System.Text.RegularExpressions;

namespace ClaimsModule.Domain.ValueObjects;

public partial record ClaimNumber
{
    public string Value { get; }

    public ClaimNumber(string value)
    {
        if (!FormatRegex().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a valid claim number. Expected format CLM-YYYY-NNNNNNN (or a legacy 6-digit CLM-YYYY-NNNNNN).", nameof(value));
        }

        Value = value;
    }

    public static ClaimNumber Create(int year, long sequence) => new($"CLM-{year}-{sequence:D7}");

    public override string ToString() => Value;

    // New numbers are always 7-digit (Create). 6-digit numbers issued before the format change
    // still load: a claim number is a business identifier and is never rewritten.
    [GeneratedRegex(@"^CLM-\d{4}-\d{6,7}$")]
    private static partial Regex FormatRegex();
}
