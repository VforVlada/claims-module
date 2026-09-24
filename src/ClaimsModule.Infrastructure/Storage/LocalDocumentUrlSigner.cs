using System.Security.Cryptography;
using System.Text;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>Shared by LocalFileSystemStorageService (signs) and DocumentsController (validates).</summary>
public static class LocalDocumentUrlSigner
{
    public static string Sign(string secret, string blobPath, long expiresAtUnix)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{blobPath}:{expiresAtUnix}"));
        return Convert.ToBase64String(hash);
    }

    public static bool IsValid(string secret, string blobPath, long expiresAtUnix, string signature)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresAtUnix)
        {
            return false;
        }

        var expected = Sign(secret, blobPath, expiresAtUnix);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }
}
