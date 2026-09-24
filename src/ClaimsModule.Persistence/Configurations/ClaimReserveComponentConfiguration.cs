using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ClaimReserveComponentConfiguration : IEntityTypeConfiguration<ClaimReserveComponent>
{
    public void Configure(EntityTypeBuilder<ClaimReserveComponent> builder)
    {
        builder.ToTable("ClaimReserveComponents");
        builder.HasKey(rc => rc.Id);

        builder.Property(rc => rc.ComponentType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(rc => rc.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(rc => rc.UserModified).HasMaxLength(255);
        builder.Property(rc => rc.RowVersion).IsRowVersion();

        builder.ComplexProperty(rc => rc.CurrentAmount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("CurrentAmount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.HasMany(rc => (IEnumerable<ReserveHistory>)rc.History)
            .WithOne()
            .HasForeignKey(h => h.ReserveComponentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(rc => rc.History).HasField("_history").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
