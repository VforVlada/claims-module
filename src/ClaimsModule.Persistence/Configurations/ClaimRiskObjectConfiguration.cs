using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ClaimRiskObjectConfiguration : IEntityTypeConfiguration<ClaimRiskObject>
{
    public void Configure(EntityTypeBuilder<ClaimRiskObject> builder)
    {
        builder.ToTable("ClaimRiskObjects");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.AssetType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(1000).IsRequired();
        builder.Property(r => r.Identifier).HasMaxLength(255);
        builder.Property(r => r.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(r => r.UserModified).HasMaxLength(255);
    }
}
