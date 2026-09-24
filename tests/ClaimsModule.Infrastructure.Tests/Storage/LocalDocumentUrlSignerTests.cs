using ClaimsModule.Infrastructure.Storage;

namespace ClaimsModule.Infrastructure.Tests.Storage;

public class LocalDocumentUrlSignerTests
{
    private const string Secret = "unit-test-secret";

    [Fact]
    public void IsValid_MatchingSignatureAndNotExpired_ReturnsTrue()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var signature = LocalDocumentUrlSigner.Sign(Secret, "org/claim/file.pdf", expiresAt);

        var isValid = LocalDocumentUrlSigner.IsValid(Secret, "org/claim/file.pdf", expiresAt, signature);

        Assert.True(isValid);
    }

    [Fact]
    public void IsValid_Expired_ReturnsFalse()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var signature = LocalDocumentUrlSigner.Sign(Secret, "org/claim/file.pdf", expiresAt);

        var isValid = LocalDocumentUrlSigner.IsValid(Secret, "org/claim/file.pdf", expiresAt, signature);

        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_TamperedPath_ReturnsFalse()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var signature = LocalDocumentUrlSigner.Sign(Secret, "org/claim/file.pdf", expiresAt);

        var isValid = LocalDocumentUrlSigner.IsValid(Secret, "org/claim/other-file.pdf", expiresAt, signature);

        Assert.False(isValid);
    }

    [Fact]
    public void IsValid_WrongSecret_ReturnsFalse()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var signature = LocalDocumentUrlSigner.Sign(Secret, "org/claim/file.pdf", expiresAt);

        var isValid = LocalDocumentUrlSigner.IsValid("different-secret", "org/claim/file.pdf", expiresAt, signature);

        Assert.False(isValid);
    }

    [Fact]
    public void Sign_IsDeterministicForSameInputs()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        var a = LocalDocumentUrlSigner.Sign(Secret, "org/claim/file.pdf", expiresAt);
        var b = LocalDocumentUrlSigner.Sign(Secret, "org/claim/file.pdf", expiresAt);

        Assert.Equal(a, b);
    }
}
