using ClaimsModule.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ClaimsModule.Persistence;

/// <summary>
/// Used only by `dotnet ef` design-time tooling (migrations add/update) so it doesn't need
/// to spin up the full API host's DI container. Never used at runtime.
/// </summary>
public sealed class ClaimsDbContextFactory : IDesignTimeDbContextFactory<ClaimsDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=ClaimsModule;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public ClaimsDbContext CreateDbContext(string[] args)
    {
        // Lets `dotnet ef` run against a non-LocalDB target (e.g. the docker-compose SQL Server
        // container) via the same ConnectionStrings__ClaimsDatabase env var ASP.NET Core's
        // configuration binder uses at runtime, without touching local (LocalDB) usage.
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__ClaimsDatabase") ?? DesignTimeConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<ClaimsDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ClaimsDbContext(optionsBuilder.Options, new DesignTimeCurrentUserService(), new DesignTimeDateTimeProvider());
    }

    private sealed class DesignTimeCurrentUserService : ICurrentUserService
    {
        public string UserId => "design-time";
        public string UserName => "design-time";
        public Guid OrganizationEntityId => Guid.Empty;
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }

    private sealed class DesignTimeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
