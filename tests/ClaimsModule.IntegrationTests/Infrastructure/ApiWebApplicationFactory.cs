using ClaimsModule.Infrastructure.Auth;
using ClaimsModule.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Respawn;
using Testcontainers.Azurite;
using Testcontainers.MsSql;

namespace ClaimsModule.IntegrationTests.Infrastructure;

/// <summary>
/// One real SQL Server container plus one Azurite container (Testcontainers), shared across the
/// whole integration test run — see IntegrationTestCollection. Deliberately not the EF InMemory
/// provider: the plan this project implements is explicit that InMemory silently ignores
/// rowversion, sequences, unique constraints and DECIMAL precision, which are exactly what these
/// tests verify.
/// </summary>
public class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Matches ClaimsModule.Persistence.Seed.SeedIds.DefaultOrganization (internal, so duplicated here).</summary>
    public static readonly Guid DefaultOrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    /// <summary>Matches SeedIds.SecondOrganization.</summary>
    public static readonly Guid OrgBId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public const string TestSigningKey = "integration-test-signing-key-0123456789abcdef";
    public const string LocalSigningSecret = "integration-test-local-signing-secret";

    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
        .WithInMemoryPersistence()
        .Build();
    private Respawner? _respawner;

    /// <summary>"AzureBlob" (Azurite) by default; LocalFileSystemApiWebApplicationFactory flips it (I-ST-04).</summary>
    protected virtual string StorageProvider => "AzureBlob";

    public string LocalStorageRoot { get; } = Path.Combine(Path.GetTempPath(), "claims-it-" + Guid.NewGuid().ToString("N"));

    public string AzuriteConnectionString => _azuriteContainer.GetConnectionString();

    // MsSqlContainer.GetConnectionString() targets the "master" system database by default —
    // point at a dedicated one instead so this run's data can never be confused with anything
    // else that might connect to the same container.
    public string ConnectionString => new SqlConnectionStringBuilder(_sqlContainer.GetConnectionString())
    {
        InitialCatalog = "ClaimsModuleTests"
    }.ConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": no appsettings.Development.json (which points at the developer's
        // LocalDB) and the Hangfire dashboard stays off, as in production (I-JOB-11).
        // UseSetting rather than ConfigureAppConfiguration: Program.cs reads the connection
        // string eagerly while building services, before late-added config sources apply.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ClaimsDatabase", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Hangfire:ServerEnabled", "false");
        builder.UseSetting("Storage:Provider", StorageProvider);
        builder.UseSetting("Storage:AzureBlobConnectionString", AzuriteConnectionString);
        builder.UseSetting("Storage:AzureBlobContainerName", "claim-documents");
        builder.UseSetting("Storage:LocalFileSystemRootPath", LocalStorageRoot);
        builder.UseSetting("Storage:LocalFileSystemSigningSecret", LocalSigningSecret);
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_sqlContainer.StartAsync(), _azuriteContainer.StartAsync());

        await using (var masterConnection = new SqlConnection(_sqlContainer.GetConnectionString()))
        {
            await masterConnection.OpenAsync();
            await using var createDatabase = masterConnection.CreateCommand();
            createDatabase.CommandText = "IF DB_ID('ClaimsModuleTests') IS NULL CREATE DATABASE [ClaimsModuleTests];";
            await createDatabase.ExecuteNonQueryAsync();
        }

        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ClaimsDbContext>();
            await context.Database.MigrateAsync();
        }

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            // Reference/config data seeded by migrations (HasData), not per-test fixtures —
            // and the whole Hangfire schema, which manages its own tables independently.
            SchemasToInclude = ["dbo"],
            TablesToIgnore = ["ClaimStatusTransitions", "CauseOfLossCodes", "Policies", "PolicyCoverages", "__EFMigrationsHistory"]
        });
    }

    /// <summary>Resets every test-owned table to empty — call before each test for isolation (I-DB tests share one container for speed).</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await _respawner!.ResetAsync(connection);
    }

    public HttpClient CreateAuthenticatedClient(string userId, string userName, string role, Guid? organizationId = null)
    {
        var jwtTokenService = Services.GetRequiredService<IJwtTokenService>();
        var token = jwtTokenService.GenerateToken(userId, userName, role, organizationId ?? DefaultOrganizationId);

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateHandlerClient() => CreateAuthenticatedClient("user-handler-1", "Hannah Handler", RoleNamesForTests.Handler);

    public HttpClient CreateSupervisorClient() => CreateAuthenticatedClient("user-supervisor-1", "Sam Supervisor", RoleNamesForTests.Supervisor);

    public HttpClient CreateManagerClient() => CreateAuthenticatedClient("user-manager-1", "Morgan Manager", RoleNamesForTests.Manager);

    public HttpClient CreateOrgBClient() => CreateAuthenticatedClient("user-orgb-handler-1", "Blake OrgB", RoleNamesForTests.Handler, OrgBId);

    /// <summary>Number of Hangfire jobs (any state) whose serialized arguments mention the given id.</summary>
    public async Task<int> CountEnqueuedJobsMentioningAsync(Guid id)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM [HangFire].[Job] WHERE [Arguments] LIKE @pattern";
        command.Parameters.AddWithValue("@pattern", $"%{id}%");
        return (int)(await command.ExecuteScalarAsync())!;
    }

    public JobStorage JobStorage => Services.GetRequiredService<JobStorage>();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(_sqlContainer.DisposeAsync().AsTask(), _azuriteContainer.DisposeAsync().AsTask());
        if (Directory.Exists(LocalStorageRoot))
        {
            Directory.Delete(LocalStorageRoot, recursive: true);
        }
    }
}

/// <summary>Same stack, LocalFileSystem storage provider instead of Azurite (I-ST-04).</summary>
public sealed class LocalFileSystemApiWebApplicationFactory : ApiWebApplicationFactory
{
    protected override string StorageProvider => "LocalFileSystem";
}
