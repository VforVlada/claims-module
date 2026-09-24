using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Dev/local fallback for IStorageService. Approximates the Azure SAS URL contract with an
/// HMAC-signed, time-limited query string validated by DocumentsController's download action.
/// </summary>
public sealed class LocalFileSystemStorageService(IOptions<StorageSettings> options) : IStorageService
{
    private readonly StorageSettings _settings = options.Value;

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var fullPath = ResolvePath(blobPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        await content.CopyToAsync(fileStream, cancellationToken);

        return blobPath;
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var expiresAtUnix = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
        var signature = LocalDocumentUrlSigner.Sign(_settings.LocalFileSystemSigningSecret, blobPath, expiresAtUnix);
        var url = $"{_settings.LocalFileSystemPublicBaseUrl}?path={Uri.EscapeDataString(blobPath)}&exp={expiresAtUnix}&sig={Uri.EscapeDataString(signature)}";
        return Task.FromResult(url);
    }

    public string GetPhysicalPath(string blobPath) => ResolvePath(blobPath);

    public Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        var root = Path.Combine(AppContext.BaseDirectory, _settings.LocalFileSystemRootPath);
        Directory.CreateDirectory(root);
        return Task.CompletedTask;
    }

    private string ResolvePath(string blobPath)
    {
        var safeRelative = blobPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(AppContext.BaseDirectory, _settings.LocalFileSystemRootPath, safeRelative);
    }
}
