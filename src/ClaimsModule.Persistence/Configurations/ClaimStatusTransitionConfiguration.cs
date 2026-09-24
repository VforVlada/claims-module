using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ClaimStatusTransitionConfiguration : IEntityTypeConfiguration<ClaimStatusTransition>
{
    public void Configure(EntityTypeBuilder<ClaimStatusTransition> builder)
    {
        builder.ToTable("ClaimStatusTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.ToStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(t => t.RequiredPermission).HasMaxLength(50).IsRequired();
        builder.Property(t => t.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(t => t.UserModified).HasMaxLength(255);

        builder.HasIndex(t => new { t.FromStatus, t.ToStatus, t.RequiredPermission }).IsUnique();

        builder.HasData(ClaimStatusTransitionSeed.Transitions);
    }
}
