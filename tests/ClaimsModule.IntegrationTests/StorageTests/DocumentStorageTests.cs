using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using Azure.Storage.Blobs;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.StorageTests;

/// <summary>
/// Plan section 4.4. Written once against the IStorageService contract and run against both
/// providers (I-ST-04): Azurite (AzureBlob) and the LocalFileSystem fallback.
/// </summary>
public abstract class DocumentStorageTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    protected abstract Task<IReadOnlyList<string>> ListStoredPathsAsync(string prefix);

    protected abstract DateTimeOffset ReadExpiry(string downloadUrl);

    protected abstract Task<HttpResponseMessage> DownloadAsync(string downloadUrl);

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<(ClaimDetailDto Claim, HttpResponseMessage Response)> UploadAsync(byte[] bytes, string fileName = "estimate.pdf", string contentType = "application/pdf")
    {
        var client = Factory.CreateHandlerClient();
        var claim = await client.CreateClaimAsync();
        var response = await client.PostAsync($"/api/claims/{claim.Id}/documents", FileContent(bytes, fileName, contentType));
        return (claim, response);
    }

    /// <summary>I-ST-01: the blob lands under {orgId}/{claimId}/ in the claim-documents container, with a metadata row.</summary>
    [Fact]
    public async Task Upload_StoresBlobUnderTenantAndClaimPrefix_AndSavesMetadata()
    {
        var (claim, response) = await UploadAsync("%PDF-1.4 test"u8.ToArray());

        await ApiClientExtensions.EnsureSuccessWithBodyAsync(response);
        var prefix = $"{ApiWebApplicationFactory.DefaultOrganizationId}/{claim.Id}/";
        var stored = Assert.Single(await ListStoredPathsAsync(prefix));
        Assert.EndsWith("estimate.pdf", stored);
        await using var context = Factory.CreateDbContext();
        var document = await context.ClaimDocuments.SingleAsync(d => d.ClaimId == claim.Id);
        Assert.Equal(stored, document.BlobPath);
        Assert.Equal("application/pdf", document.ContentType);
    }

    /// <summary>§3.2: documents carry a DocumentType (defaulting to Other) through upload and listing.</summary>
    [Fact]
    public async Task Upload_WithDocumentType_IsListedWithThatType()
    {
        var client = Factory.CreateHandlerClient();
        var claim = await client.CreateClaimAsync();
        var typed = FileContent("%PDF"u8.ToArray(), "police.pdf", "application/pdf");
        typed.Add(new StringContent(nameof(Domain.Enums.DocumentType.PoliceReport)), "documentType");

        (await client.PostAsync($"/api/claims/{claim.Id}/documents", typed)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/claims/{claim.Id}/documents", FileContent("x"u8.ToArray(), "misc.txt", "text/plain"))).EnsureSuccessStatusCode();

        var documents = (await client.GetFromJsonAsync<List<ClaimDocumentDto>>($"/api/claims/{claim.Id}/documents"))!;
        Assert.Equal(Domain.Enums.DocumentType.PoliceReport, documents.Single(d => d.FileName == "police.pdf").DocumentType);
        Assert.Equal(Domain.Enums.DocumentType.Other, documents.Single(d => d.FileName == "misc.txt").DocumentType);
    }

    /// <summary>Two uploads with the same name are two documents, not an overwrite (or a crash).</summary>
    [Fact]
    public async Task Upload_SameFileNameTwice_KeepsBothDocuments()
    {
        var client = Factory.CreateHandlerClient();
        var claim = await client.CreateClaimAsync();

        (await client.PostAsync($"/api/claims/{claim.Id}/documents", FileContent("first"u8.ToArray(), "notes.txt", "text/plain"))).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/claims/{claim.Id}/documents", FileContent("second"u8.ToArray(), "notes.txt", "text/plain"))).EnsureSuccessStatusCode();

        Assert.Equal(2, (await ListStoredPathsAsync($"{ApiWebApplicationFactory.DefaultOrganizationId}/{claim.Id}/")).Count);
    }

    /// <summary>I-ST-02 and I-ST-03: listed documents carry a ~1h signed URL that downloads the exact bytes.</summary>
    [Fact]
    public async Task ListDocuments_SignedUrlExpiresInAboutAnHour_AndDownloadsTheUploadedContent()
    {
        var content = "claim evidence, byte for byte"u8.ToArray();
        var (claim, upload) = await UploadAsync(content, "evidence.txt", "text/plain");
        await ApiClientExtensions.EnsureSuccessWithBodyAsync(upload);

        var documents = await Factory.CreateHandlerClient().GetFromJsonAsync<List<ClaimDocumentDto>>($"/api/claims/{claim.Id}/documents");

        var url = Assert.Single(documents!).DownloadUrl;
        Assert.InRange(ReadExpiry(url), DateTimeOffset.UtcNow.AddMinutes(55), DateTimeOffset.UtcNow.AddMinutes(65));
        var download = await DownloadAsync(url);
        download.EnsureSuccessStatusCode();
        Assert.Equal(content, await download.Content.ReadAsByteArrayAsync());
    }

    /// <summary>I-ST-05: disallowed type or oversized file → 400, nothing stored.</summary>
    [Fact]
    public async Task Upload_DisallowedContentType_Returns400()
    {
        var (claim, response) = await UploadAsync("MZ"u8.ToArray(), "tool.exe", "application/x-msdownload");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ListStoredPathsAsync($"{ApiWebApplicationFactory.DefaultOrganizationId}/{claim.Id}/"));
    }

    [Fact]
    public async Task Upload_OneByteOverTheLimit_Returns400()
    {
        var (claim, response) = await UploadAsync(new byte[Application.Common.DocumentPolicy.MaxSizeBytes + 1]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ListStoredPathsAsync($"{ApiWebApplicationFactory.DefaultOrganizationId}/{claim.Id}/"));
    }

    private sealed class FailingStorageService : IStorageService
    {
        public Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken cancellationToken) =>
            throw new IOException("Simulated storage outage.");

        public Task<string> GetDownloadUrlAsync(string blobPath, TimeSpan ttl, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task CheckHealthAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>I-ST-06: if storage fails, no orphaned metadata row points at a blob that doesn't exist.</summary>
    [Fact]
    public async Task Upload_StorageThrows_LeavesNoOrphanMetadataRow()
    {
        var claim = await Factory.CreateHandlerClient().CreateClaimAsync();
        using var failingApp = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IStorageService, FailingStorageService>()));
        var client = failingApp.CreateClient();
        client.DefaultRequestHeaders.Authorization = Factory.CreateHandlerClient().DefaultRequestHeaders.Authorization;

        var response = await client.PostAsync($"/api/claims/{claim.Id}/documents", FileContent("x"u8.ToArray(), "a.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await using var context = Factory.CreateDbContext();
        Assert.False(await context.ClaimDocuments.IgnoreQueryFilters().AnyAsync(d => d.ClaimId == claim.Id));
    }
}

[Collection(IntegrationTestCollection.Name)]
public sealed class AzureBlobDocumentStorageTests(ApiWebApplicationFactory factory) : DocumentStorageTests(factory)
{
    private BlobContainerClient Container => new(Factory.AzuriteConnectionString, "claim-documents");

    protected override async Task<IReadOnlyList<string>> ListStoredPathsAsync(string prefix)
    {
        if (!await Container.ExistsAsync())
        {
            return [];
        }

        var names = new List<string>();
        await foreach (var blob in Container.GetBlobsAsync(Azure.Storage.Blobs.Models.BlobTraits.None, Azure.Storage.Blobs.Models.BlobStates.None, prefix, CancellationToken.None))
        {
            names.Add(blob.Name);
        }

        return names;
    }

    /// <summary>The SAS "se" (signed expiry) query parameter.</summary>
    protected override DateTimeOffset ReadExpiry(string downloadUrl) =>
        DateTimeOffset.Parse(HttpUtility.ParseQueryString(new Uri(downloadUrl).Query)["se"]!);

    protected override Task<HttpResponseMessage> DownloadAsync(string downloadUrl) => new HttpClient().GetAsync(downloadUrl);
}

[Collection(LocalStorageIntegrationTestCollection.Name)]
public sealed class LocalFileSystemDocumentStorageTests(LocalFileSystemApiWebApplicationFactory factory) : DocumentStorageTests(factory)
{
    protected override Task<IReadOnlyList<string>> ListStoredPathsAsync(string prefix)
    {
        var directory = Path.Combine(Factory.LocalStorageRoot, prefix);
        IReadOnlyList<string> paths = Directory.Exists(directory)
            ? Directory.GetFiles(directory).Select(f => Path.GetRelativePath(Factory.LocalStorageRoot, f).Replace('\\', '/')).ToList()
            : [];
        return Task.FromResult(paths);
    }

    /// <summary>The HMAC-signed URL's "exp" (unix seconds) query parameter.</summary>
    protected override DateTimeOffset ReadExpiry(string downloadUrl) =>
        DateTimeOffset.FromUnixTimeSeconds(long.Parse(HttpUtility.ParseQueryString(new Uri(new Uri("http://localhost"), downloadUrl).Query)["exp"]!));

    protected override Task<HttpResponseMessage> DownloadAsync(string downloadUrl) => Factory.CreateClient().GetAsync(downloadUrl);
}
