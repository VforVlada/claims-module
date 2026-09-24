using ClaimsModule.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace ClaimsModule.Infrastructure.Tests.Storage;

public class LocalFileSystemStorageServiceTests : IDisposable
{
    private readonly string _rootPath = $"test-storage/{Guid.NewGuid()}";
    private readonly StorageSettings _settings;
    private readonly LocalFileSystemStorageService _sut;

    public LocalFileSystemStorageServiceTests()
    {
        _settings = new StorageSettings
        {
            LocalFileSystemRootPath = _rootPath,
            LocalFileSystemPublicBaseUrl = "/api/claims/local-documents",
            LocalFileSystemSigningSecret = "unit-test-secret"
        };
        _sut = new LocalFileSystemStorageService(Options.Create(_settings));
    }

    [Fact]
    public async Task UploadAsync_WritesFileToDiskAtBlobPath()
    {
        var content = "hello world"u8.ToArray();
        using var stream = new MemoryStream(content);

        var blobPath = await _sut.UploadAsync("org/claim/photo.jpg", stream, "image/jpeg", CancellationToken.None);

        Assert.Equal("org/claim/photo.jpg", blobPath);
        var physicalPath = _sut.GetPhysicalPath(blobPath);
        Assert.True(File.Exists(physicalPath));
        Assert.Equal(content, await File.ReadAllBytesAsync(physicalPath));
    }

    [Fact]
    public async Task GetDownloadUrlAsync_ReturnsUrlWithValidSignature()
    {
        var url = await _sut.GetDownloadUrlAsync("org/claim/photo.jpg", TimeSpan.FromHours(1), CancellationToken.None);

        Assert.StartsWith("/api/claims/local-documents?path=", url);

        var queryParams = new Uri("http://localhost" + url).Query.TrimStart('?')
            .Split('&')
            .Select(p => p.Split('=', 2))
            .ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));

        var path = queryParams["path"];
        var exp = long.Parse(queryParams["exp"]);
        var sig = queryParams["sig"];

        Assert.True(LocalDocumentUrlSigner.IsValid(_settings.LocalFileSystemSigningSecret, path, exp, sig));
    }

    [Fact]
    public void GetPhysicalPath_ResolvesUnderConfiguredRoot()
    {
        var physicalPath = _sut.GetPhysicalPath("org/claim/photo.jpg");
        var expectedPath = Path.Combine(AppContext.BaseDirectory, _rootPath, "org", "claim", "photo.jpg");

        Assert.Equal(expectedPath, physicalPath);
    }

    public void Dispose()
    {
        var fullRoot = Path.Combine(AppContext.BaseDirectory, _rootPath);
        if (Directory.Exists(fullRoot))
        {
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
