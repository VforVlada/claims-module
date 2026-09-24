namespace ClaimsModule.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<ApiWebApplicationFactory>
{
    public const string Name = "Integration";
}

/// <summary>Same stack with the LocalFileSystem storage provider — its own containers, so it can run alongside the main collection.</summary>
[CollectionDefinition(Name)]
public sealed class LocalStorageIntegrationTestCollection : ICollectionFixture<LocalFileSystemApiWebApplicationFactory>
{
    public const string Name = "IntegrationLocalStorage";
}
