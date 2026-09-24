namespace ClaimsModule.Infrastructure.Storage;

public sealed class StorageSettings
{
    public const string SectionName = "Storage";

    /// <summary>"AzureBlob" or "LocalFileSystem".</summary>
    public string Provider { get; set; } = "LocalFileSystem";

    public string? AzureBlobConnectionString { get; set; }

    public string AzureBlobContainerName { get; set; } = "claim-documents";

    public string LocalFileSystemRootPath { get; set; } = "App_Data/claim-documents";

    /// <summary>Base URL the API serves local files back from (dev-only fallback for the Azure SAS URL contract).</summary>
    public string LocalFileSystemPublicBaseUrl { get; set; } = "/api/claims/local-documents";

    /// <summary>HMAC secret for signing local-fallback download URLs (dev-only; not used by AzureBlob).</summary>
    public string LocalFileSystemSigningSecret { get; set; } = "local-dev-only-signing-secret-change-me";
}
