using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Infrastructure.Tests.TestHelpers;

/// <summary>
/// EF Core stand-in for ClaimsDbContext (Persistence project) — mirrors just enough of its
/// model configuration to exercise background jobs against IApplicationDbContext. Backed by
/// SQLite-in-memory rather than the EF Core InMemory provider, which cannot materialize any
/// entity with a ComplexProperty (Money) at all — even without Include (a known, still-open
/// EF Core limitation, dotnet/efcore#31464). The real ClaimsDbContext can't be reused
/// directly here: it's sealed, and its OnModelCreating unconditionally declares a SQL Server
/// sequence (ClaimNumberSequence) that SQLite's migrations generator rejects outright.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options, IDateTimeProvider dateTimeProvider) : DbContext(options), IApplicationDbContext
{
    private SqliteConnection? _connection;

    public DbSet<Claim> Claims => Set<Claim>();

    public DbSet<ClaimParty> ClaimParties => Set<ClaimParty>();

    public DbSet<ClaimRiskObject> ClaimRiskObjects => Set<ClaimRiskObject>();

    public DbSet<ClaimReserveComponent> ClaimReserveComponents => Set<ClaimReserveComponent>();

    public DbSet<ReserveHistory> ReserveHistories => Set<ReserveHistory>();

    public DbSet<ClaimDocument> ClaimDocuments => Set<ClaimDocument>();

    public DbSet<ClaimAuditLog> ClaimAuditLogs => Set<ClaimAuditLog>();

    public DbSet<CauseOfLossCode> CauseOfLossCodes => Set<CauseOfLossCode>();

    public DbSet<ClaimStatusTransition> ClaimStatusTransitions => Set<ClaimStatusTransition>();

    public DbSet<Policy> Policies => Set<Policy>();

    public DbSet<PolicyCoverage> PolicyCoverages => Set<PolicyCoverage>();

    public IReadOnlyCollection<IHasDomainEvents> GetEntitiesWithDomainEvents() => ChangeTracker
        .Entries<IHasDomainEvents>()
        .Select(e => e.Entity)
        .Where(e => e.DomainEvents.Count != 0)
        .ToList();

    // Mirrors ClaimsDbContext.SaveChangesAsync's audit-column stamping — background job
    // tests (e.g. SLA staleness) depend on CreatedAt/UpdatedAt reflecting the fake clock.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.Entity<Claim>(builder =>
        {
            builder.Property(c => c.ClaimNumber).HasConversion(v => v.Value, v => new ClaimNumber(v));

            builder.HasOne(c => c.LossEvent).WithOne().HasForeignKey<LossEvent>(le => le.ClaimId).IsRequired();

            builder.HasMany(c => (IEnumerable<ClaimParty>)c.Parties).WithOne().HasForeignKey("ClaimId");
            builder.Navigation(c => c.Parties).HasField("_parties").UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(c => (IEnumerable<ClaimRiskObject>)c.RiskObjects).WithOne().HasForeignKey("ClaimId");
            builder.Navigation(c => c.RiskObjects).HasField("_riskObjects").UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(c => (IEnumerable<ClaimReserveComponent>)c.ReserveComponents).WithOne().HasForeignKey("ClaimId");
            builder.Navigation(c => c.ReserveComponents).HasField("_reserveComponents").UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(c => (IEnumerable<ClaimDocument>)c.Documents).WithOne().HasForeignKey("ClaimId");
            builder.Navigation(c => c.Documents).HasField("_documents").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ClaimReserveComponent>(builder =>
        {
            builder.ComplexProperty(rc => rc.CurrentAmount, money =>
            {
                money.Property(m => m.Amount);
                money.Property(m => m.Currency);
            });

            builder.HasMany(rc => (IEnumerable<ReserveHistory>)rc.History).WithOne().HasForeignKey(h => h.ReserveComponentId);
            builder.Navigation(rc => rc.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ReserveHistory>(builder =>
        {
            builder.ComplexProperty(h => h.Amount, money =>
            {
                money.Property(m => m.Amount);
                money.Property(m => m.Currency);
            });
            builder.ComplexProperty(h => h.PreviousAmount, money =>
            {
                money.Property(m => m.Amount);
                money.Property(m => m.Currency);
            });
            builder.ComplexProperty(h => h.NewAmount, money =>
            {
                money.Property(m => m.Amount);
                money.Property(m => m.Currency);
            });
        });

        // Mirrors ClaimAuditLogConfiguration's unique filtered index — PostGlReserveChangeJobTests
        // relies on this to exercise the real idempotency-key-collision guard (SQLite enforces it;
        // the InMemory provider used by CreateInMemory() ignores relational annotations like this).
        modelBuilder.Entity<ClaimAuditLog>(builder =>
        {
            builder.HasIndex(a => a.IdempotencyKey).IsUnique().HasFilter("IdempotencyKey IS NOT NULL");
        });

        modelBuilder.Entity<PolicyCoverage>(builder =>
        {
            builder.ComplexProperty(c => c.Limit, money =>
            {
                money.Property(m => m.Amount);
                money.Property(m => m.Currency);
            });
            builder.ComplexProperty(c => c.Deductible, money =>
            {
                money.Property(m => m.Amount);
                money.Property(m => m.Currency);
            });
        });

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public static TestDbContext Create(IDateTimeProvider? dateTimeProvider = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDbContext(options, dateTimeProvider ?? new FakeDateTimeProvider()) { _connection = connection };
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// For tests that never touch a ComplexProperty entity (Claim/ClaimAuditLog have none):
    /// EF Core's SQLite provider can't translate the "(UpdatedAt ?? CreatedAt) &lt; x"
    /// comparison SlaMonitoringJob's query uses, so it needs the InMemory provider instead
    /// (SQL Server, used in production, has no trouble with this pattern).
    /// </summary>
    public static TestDbContext CreateInMemory(IDateTimeProvider? dateTimeProvider = null)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestDbContext(options, dateTimeProvider ?? new FakeDateTimeProvider());
    }

    public override void Dispose()
    {
        base.Dispose();
        _connection?.Dispose();
    }
}
