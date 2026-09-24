namespace ClaimsModule.Application.Common.Constants;

/// <summary>
/// Upper bound on a single reserve amount (by absolute value, so it also bounds negative recovery
/// reserves). Without it an oversized amount reaches SQL Server's decimal(19,4) money columns and
/// fails as an unhandled 500 instead of a validation error. Mirrored by the frontend's
/// reserve-amount validator.
/// </summary>
public static class ReserveLimits
{
    public const decimal MaxAmount = 1_000_000_000m;

    public const string MaxAmountMessage = "Reserve amount cannot exceed 1,000,000,000.";
}
