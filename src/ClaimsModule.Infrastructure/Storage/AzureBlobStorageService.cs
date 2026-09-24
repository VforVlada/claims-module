using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using ClaimsModule.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Storage;

public sealed class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(IOptions<StorageSettings> options)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.AzureBlobConnectionString))
        {
            throw new InvalidOperationException("Storage:AzureBlobConnectionString is required when Storage:Provider is 'AzureBlob'.");
        }

        _containerClient = new BlobContainerClient(settings.AzureBlobConnectionString, settings.AzureBlobContainerName);
    }

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobClient = _containerClient.GetBlobClient(blobPath);
        await blobClient.UploadAsync(content, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType }, cancellationToken: cancellationToken);

        return blobPath;
    }

    public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var blobClient = _containerClient.GetBlobClient(blobPath);

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException("The configured storage credentials cannot generate a SAS URI.");
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(ttl)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return Task.FromResult(blobClient.GenerateSasUri(sasBuilder).ToString());
    }

    public async Task CheckHealthAsync(CancellationToken cancellationToken) =>
        await _containerClient.ExistsAsync(cancellationToken);
}
