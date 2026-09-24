using System.Linq.Expressions;
using System.Reflection;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Persistence;

public sealed class ClaimsDbContext(DbContextOptions<ClaimsDbContext> options, ICurrentUserService currentUser, IDateTimeProvider dateTimeProvider)
    : DbContext(options), IApplicationDbContext
{
    // Referenced by name from the manually-built query filter expressions below — EF Core
    // recognizes a ConstantExpression holding "this" (the DbContext instance) inside a query
    // filter and re-binds it to whichever context instance is actually executing the query, so
    // this always reflects the current request's user, never the value at model-build time.
    private Guid CurrentOrganizationId => currentUser.OrganizationEntityId;

    /// <summary>
    /// Shared workflow configuration, not tenant-owned data — seeded once for the whole system
    /// (see ClaimStatusTransitionSeed) rather than per organization, so it is exempt from the
    /// tenant filter below. Every other BaseEntity-derived table is tenant-isolated.
    /// </summary>
    private static readonly Type[] TenantFilterExemptTypes = [typeof(ClaimStatusTransition)];


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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // AggregateRoot.DomainEvents is an in-memory-only concern (cleared by
        // DomainEventDispatchBehavior post-commit) — never mapped to a column/table.
        modelBuilder.Ignore<DomainEvent>();

        modelBuilder.HasSequence<long>("ClaimNumberSequence", schema: "dbo").StartsAt(1).IncrementsBy(1);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var isDeletedProperty = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var notDeletedCondition = Expression.Equal(isDeletedProperty, Expression.Constant(false));

                Expression condition = notDeletedCondition;
                if (!TenantFilterExemptTypes.Contains(entityType.ClrType))
                {
                    var entityOrgId = Expression.Property(parameter, nameof(BaseEntity.OrganizationEntityId));
                    var currentOrgId = Expression.Property(Expression.Constant(this), nameof(CurrentOrganizationId));
                    var tenantCondition = Expression.Equal(entityOrgId, currentOrgId);
                    condition = Expression.AndAlso(notDeletedCondition, tenantCondition);
                }

                var lambda = Expression.Lambda(condition, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);

                // BaseEntity.Id is always assigned client-side (Guid.NewGuid() in the property
                // initializer), never by the database. Without this, EF's default GUID-key
                // convention (ValueGeneratedOnAdd) makes it treat a new child entity discovered
                // via navigation fixup on an already-tracked aggregate (e.g. adding a reserve to
                // a loaded Claim) as an existing row to UPDATE rather than INSERT, since a
                // non-default key looks like it must have come from the database.
                modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.Id)).ValueGeneratedNever();
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        // The audit log is append-only (DB-07): no repository exposes an update or delete, and
        // this closes the remaining door — mutating a tracked row and saving.
        if (ChangeTracker.Entries<ClaimAuditLog>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Claim audit log entries are append-only and cannot be modified or deleted.");
        }

        // Soft delete: a Remove() never physically deletes a row — it becomes IsDeleted/DeletedAt,
        // which the global query filter then hides. (Runs after the audit-log guard above, so an
        // audit row still can't be deleted even softly.) Cascade-deleted dependents are converted too.
        foreach (var entry in ChangeTracker.Entries<BaseEntity>().Where(e => e.State == EntityState.Deleted).ToList())
        {
            entry.State = EntityState.Modified;
            entry.Entity.MarkDeleted(now);
        }

        // Background jobs have no current user; keep the actor they set ("system") rather than blanking it.
        var actor = string.IsNullOrEmpty(currentUser.UserName) ? null : currentUser.UserName;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // A tenant-owned row without a tenant is invisible to everyone once saved (the
                    // query filter matches no organization) — fail loudly instead of losing data.
                    if (entry.Entity.OrganizationEntityId == Guid.Empty && !TenantFilterExemptTypes.Contains(entry.Entity.GetType()))
                    {
                        throw new InvalidOperationException($"{entry.Entity.GetType().Name} {entry.Entity.Id} has no OrganizationEntityId.");
                    }

                    entry.Entity.CreatedAt = now;
                    entry.Entity.UserCreated = actor ?? entry.Entity.UserCreated;
                    break;
                case EntityState.Modified when IsSlaBookkeepingOnly(entry):
                    // Flagging a breach is not activity: stamping UpdatedAt here would reset the
                    // very staleness clock the SLA job measures.
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UserModified = actor ?? "system";
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static readonly string[] SlaBookkeepingProperties = [nameof(Claim.IsSlaBreached), nameof(Claim.SlaBreachedAt)];

    private static bool IsSlaBookkeepingOnly(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseEntity> entry) =>
        entry.Entity is Claim && entry.Properties.Where(p => p.IsModified).All(p => SlaBookkeepingProperties.Contains(p.Metadata.Name));

    public void DiscardChanges() => ChangeTracker.Clear();

    public IReadOnlyCollection<IHasDomainEvents> GetEntitiesWithDomainEvents() => ChangeTracker
        .Entries<IHasDomainEvents>()
        .Select(e => e.Entity)
        .Where(e => e.DomainEvents.Count != 0)
        .ToList();
}
