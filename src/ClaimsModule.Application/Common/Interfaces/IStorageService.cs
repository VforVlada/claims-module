namespace ClaimsModule.Application.Common.Interfaces;

public interface IStorageService
{
    Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>Returns a time-limited download URL; document bytes are never proxied through the API.</summary>
    Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Backs the /health endpoint's storage check (S-01) — throws if the provider is unreachable.</summary>
    Task CheckHealthAsync(CancellationToken cancellationToken);
}
