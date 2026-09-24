namespace ClaimsModule.IntegrationTests.Infrastructure;

public abstract class IntegrationTestBase(ApiWebApplicationFactory factory) : IAsyncLifetime
{
    protected ApiWebApplicationFactory Factory { get; } = factory;

    public Task InitializeAsync() => Factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;
}
