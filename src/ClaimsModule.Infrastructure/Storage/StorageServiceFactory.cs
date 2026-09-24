using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

/// <summary>
/// Chooses the IStorageService from <c>Storage:Provider</c> (brief §3.6).
/// <list type="bullet">
/// <item><c>LocalFileSystem</c>: always local disk.</item>
/// <item><c>AzureBlob</c> in Development: probed once at startup. If there is no connection string
/// or the account is unreachable (e.g. Azurite isn't running), fall back to local disk with a
/// warning, so local development keeps working.</item>
/// <item><c>AzureBlob</c> anywhere else: Azure, with no fallback. A misconfigured deployment must
/// fail loudly (startup error, or <c>/health</c> unhealthy), not quietly store documents on the
/// App Service's disk.</item>
/// </list>
/// </summary>
public static class StorageServiceFactory
{
    public static readonly TimeSpan DefaultProbeTimeout = TimeSpan.FromSeconds(5);

    public static IStorageService Create(IOptions<StorageSettings> options, bool isDevelopment, ILogger logger, TimeSpan? probeTimeout = null)
    {
        var settings = options.Value;
        if (!string.Equals(settings.Provider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
        {
            return new LocalFileSystemStorageService(options);
        }

        if (!isDevelopment)
        {
            return new AzureBlobStorageService(options);
        }

        if (string.IsNullOrWhiteSpace(settings.AzureBlobConnectionString))
        {
            logger.LogWarning("Storage:Provider is AzureBlob but no connection string is set; using local file system storage for development.");
            return new LocalFileSystemStorageService(options);
        }

        try
        {
            var azure = new AzureBlobStorageService(options);
            using var timeout = new CancellationTokenSource(probeTimeout ?? DefaultProbeTimeout);
            azure.CheckHealthAsync(timeout.Token).GetAwaiter().GetResult();
            return azure;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Azure Blob Storage is unavailable; using local file system storage for development.");
            return new LocalFileSystemStorageService(options);
        }
    }
}
