using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Tests.Storage;

/// <summary>Brief §3.6: provider chosen by config, with a graceful local fallback in development only.</summary>
public class StorageServiceFactoryTests
{
    // Azurite's well-known development account, pointed at a port nothing listens on.
    private const string UnreachableAzure =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:1/devstoreaccount1;";

    private static IOptions<StorageSettings> Settings(string provider, string? connectionString = null) =>
        Options.Create(new StorageSettings { Provider = provider, AzureBlobConnectionString = connectionString });

    [Fact]
    public void LocalFileSystemProvider_IsLocal() =>
        Assert.IsType<LocalFileSystemStorageService>(StorageServiceFactory.Create(Settings("LocalFileSystem"), isDevelopment: false, NullLogger.Instance));

    [Fact]
    public void AzureBlob_InDevelopment_WithoutConnectionString_FallsBackToLocal() =>
        Assert.IsType<LocalFileSystemStorageService>(StorageServiceFactory.Create(Settings("AzureBlob"), isDevelopment: true, NullLogger.Instance));

    [Fact]
    public void AzureBlob_InDevelopment_Unreachable_FallsBackToLocal() =>
        Assert.IsType<LocalFileSystemStorageService>(StorageServiceFactory.Create(
            Settings("AzureBlob", UnreachableAzure), isDevelopment: true, NullLogger.Instance, probeTimeout: TimeSpan.FromSeconds(2)));

    /// <summary>Outside development there is no fallback: a deployment missing its storage configuration must fail, not silently write to local disk.</summary>
    [Fact]
    public void AzureBlob_OutsideDevelopment_WithoutConnectionString_Throws() =>
        Assert.Throws<InvalidOperationException>(() => StorageServiceFactory.Create(Settings("AzureBlob"), isDevelopment: false, NullLogger.Instance));

    [Fact]
    public void AzureBlob_OutsideDevelopment_IsAzureWithoutProbing() =>
        Assert.IsType<AzureBlobStorageService>(StorageServiceFactory.Create(Settings("AzureBlob", UnreachableAzure), isDevelopment: false, NullLogger.Instance));
}
