using ClaimsModule.Domain.Entities;
using ClaimsModule.Persistence.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class CauseOfLossCodeConfiguration : IEntityTypeConfiguration<CauseOfLossCode>
{
    public void Configure(EntityTypeBuilder<CauseOfLossCode> builder)
    {
        builder.ToTable("CauseOfLossCodes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Code).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(255).IsRequired();
        builder.Property(c => c.PerilCategory).HasMaxLength(50).IsRequired();
        builder.Property(c => c.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(c => c.UserModified).HasMaxLength(255);

        builder.HasIndex(c => c.Code).IsUnique();

        builder.HasData(CauseOfLossCodeSeed.Codes);
    }
}
