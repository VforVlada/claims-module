using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class PolicyConfiguration : IEntityTypeConfiguration<Policy>
{
    public void Configure(EntityTypeBuilder<Policy> builder)
    {
        builder.ToTable("Policies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PolicyNumber).HasMaxLength(50).IsRequired();
        builder.Property(p => p.ClientName).HasMaxLength(255).IsRequired();
        builder.Property(p => p.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(p => p.UserModified).HasMaxLength(255);

        builder.HasIndex(p => p.PolicyNumber).IsUnique();

        builder.HasMany(p => p.Coverages)
            .WithOne(c => c.Policy)
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasData(PolicySeed.Policies);
    }
}
