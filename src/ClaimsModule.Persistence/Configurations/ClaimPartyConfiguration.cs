using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ClaimPartyConfiguration : IEntityTypeConfiguration<ClaimParty>
{
    public void Configure(EntityTypeBuilder<ClaimParty> builder)
    {
        builder.ToTable("ClaimParties");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PartyType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.PartyRole).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(255).IsRequired();
        builder.Property(p => p.ContactEmail).HasMaxLength(255);
        builder.Property(p => p.ContactPhone).HasMaxLength(50);
        builder.Property(p => p.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(p => p.UserModified).HasMaxLength(255);
    }
}
