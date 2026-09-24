using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ClaimConfiguration : IEntityTypeConfiguration<Claim>
{
    public void Configure(EntityTypeBuilder<Claim> builder)
    {
        builder.ToTable("Claims");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.ClaimNumber)
            .HasConversion(v => v.Value, v => new ClaimNumber(v))
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(c => c.ClaimNumber).IsUnique();

        builder.Property(c => c.ClaimType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(c => c.AssignedHandler).HasMaxLength(255).IsRequired();
        builder.Property(c => c.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(c => c.UserModified).HasMaxLength(255);

        // Shadow property: optimistic concurrency is purely a persistence concern, so the token
        // lives in the EF model only; the domain class never sees it.
        builder.Property<byte[]>("RowVersion").IsRowVersion();

        builder.HasOne(c => c.Policy)
            .WithMany()
            .HasForeignKey(c => c.PolicyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.LossEvent)
            .WithOne()
            .HasForeignKey<LossEvent>(le => le.ClaimId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasMany(c => (IEnumerable<ClaimParty>)c.Parties).WithOne().HasForeignKey("ClaimId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Parties).HasField("_parties").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => (IEnumerable<ClaimRiskObject>)c.RiskObjects).WithOne().HasForeignKey("ClaimId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.RiskObjects).HasField("_riskObjects").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => (IEnumerable<ClaimReserveComponent>)c.ReserveComponents).WithOne().HasForeignKey("ClaimId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.ReserveComponents).HasField("_reserveComponents").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => (IEnumerable<ClaimDocument>)c.Documents).WithOne().HasForeignKey("ClaimId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(c => c.Documents).HasField("_documents").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
