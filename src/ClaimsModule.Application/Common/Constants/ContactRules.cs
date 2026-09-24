namespace ClaimsModule.Application.Common.Constants;

/// <summary>Party contact-detail rules shared by the AddClaimParty and CreateClaim validators. Mirrored by the frontend's phone-input directive.</summary>
public static class ContactRules
{
    /// <summary>Digits only, with an optional leading "+" for the international prefix.</summary>
    public const string PhonePattern = @"^\+?[0-9]+$";

    public const string PhoneMessage = "Phone number may contain only digits and an optional leading '+'.";

    /// <summary>Matches the ClaimParties.ContactPhone column length.</summary>
    public const int PhoneMaxLength = 50;
}
