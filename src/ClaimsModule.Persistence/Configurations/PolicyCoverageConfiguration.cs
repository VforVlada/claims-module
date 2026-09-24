using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class PolicyCoverageConfiguration : IEntityTypeConfiguration<PolicyCoverage>
{
    public void Configure(EntityTypeBuilder<PolicyCoverage> builder)
    {
        builder.ToTable("PolicyCoverages");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CoverageType).HasMaxLength(100).IsRequired();
        builder.Property(c => c.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(c => c.UserModified).HasMaxLength(255);

        builder.ComplexProperty(c => c.Limit, money =>
        {
            money.Property(m => m.Amount).HasColumnName("LimitAmount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("LimitCurrency").HasMaxLength(3);
        });

        builder.ComplexProperty(c => c.Deductible, money =>
        {
            money.Property(m => m.Amount).HasColumnName("DeductibleAmount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("DeductibleCurrency").HasMaxLength(3);
        });

        // EF Core does not support HasData() on entities with complex properties
        // (see dotnet/efcore#31254) — PolicyCoverage rows are seeded via a raw SQL
        // script in the InitialCreate migration instead (see PolicySeed.Coverages).
    }
}
