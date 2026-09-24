using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class LossEventConfiguration : IEntityTypeConfiguration<LossEvent>
{
    public void Configure(EntityTypeBuilder<LossEvent> builder)
    {
        builder.ToTable("LossEvents");
        builder.HasKey(le => le.Id);

        builder.Property(le => le.Description).HasMaxLength(2000).IsRequired();
        builder.Property(le => le.Location).HasMaxLength(500).IsRequired();
        builder.Property(le => le.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(le => le.UserModified).HasMaxLength(255);

        builder.HasOne(le => le.CauseOfLossCode)
            .WithMany()
            .HasForeignKey(le => le.CauseOfLossCodeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
