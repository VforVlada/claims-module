namespace ClaimsModule.Application.Common.Constants;

/// <summary>ISO 4217-style currency code rule shared by every reserve validator. Mirrored by the frontend's currency-input directive.</summary>
public static class CurrencyRules
{
    /// <summary>Exactly three letters, e.g. "USD" (Money upper-cases it).</summary>
    public const string Pattern = "^[A-Za-z]{3}$";

    public const string Message = "Currency must be a 3-letter code, e.g. USD.";
}
