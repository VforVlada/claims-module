using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ClaimAuditLogConfiguration : IEntityTypeConfiguration<ClaimAuditLog>
{
    public void Configure(EntityTypeBuilder<ClaimAuditLog> builder)
    {
        builder.ToTable("ClaimAuditLogs");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.OldValues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.NewValues).HasColumnType("nvarchar(max)");
        builder.Property(a => a.PerformedBy).HasMaxLength(255).IsRequired();
        builder.Property(a => a.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(a => a.UserModified).HasMaxLength(255);
        builder.Property(a => a.IdempotencyKey).HasMaxLength(200);

        builder.HasIndex(a => new { a.ClaimId, a.CreatedAt });

        // Filtered so only dedup-guarded entries (e.g. GL postings) are constrained; most
        // audit rows leave IdempotencyKey null and are unaffected.
        builder.HasIndex(a => a.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
    }
}
