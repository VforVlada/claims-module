using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.ValueObjects;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Tests.TestHelpers;

/// <summary>
/// EF Core stand-in for ClaimsDbContext (which lives in the Persistence project, not
/// referenced here) — mirrors just enough of its model configuration to exercise handlers
/// and domain services against IApplicationDbContext. Backed by SQLite-in-memory rather
/// than the EF Core InMemory provider: the InMemory provider's query shaper cannot handle
/// ComplexProperty (Money) inside a collection reached via Include().ThenInclude() and
/// throws KeyNotFoundException (a known, still-open EF Core limitation — dotnet/efcore#31464).
/// SQLite is a real relational provider and exercises the same code path as SQL Server.
/// </summary>
public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options), IApplicationDbContext
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

    public void DiscardChanges() => ChangeTracker.Clear();

    public IReadOnlyCollection<IHasDomainEvents> GetEntitiesWithDomainEvents() => ChangeTracker
        .Entries<IHasDomainEvents>()
        .Select(e => e.Entity)
        .Where(e => e.DomainEvents.Count != 0)
        .ToList();

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

    public static TestDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        // Tests build arbitrary aggregates in isolation (e.g. a claim referencing a
        // CauseOfLossCodeId that a given test never seeds) — they exercise handler/domain
        // logic, not full referential integrity across every reference table, so FK
        // enforcement (which SQLite, unlike the old EF InMemory provider, actually applies)
        // is turned off here.
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = OFF;";
            pragma.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestDbContext(options) { _connection = connection };
        context.Database.EnsureCreated();
        return context;
    }

    public override void Dispose()
    {
        base.Dispose();
        _connection?.Dispose();
    }
}
