using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ReserveHistoryConfiguration : IEntityTypeConfiguration<ReserveHistory>
{
    public void Configure(EntityTypeBuilder<ReserveHistory> builder)
    {
        builder.ToTable("ReserveHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.RequiredTier).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.ApprovalStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.PostingStatus).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(h => h.PostingJobId).HasMaxLength(100);
        builder.Property(h => h.RejectionReason).HasMaxLength(1000);
        builder.Property(h => h.RequestedBy).HasMaxLength(255).IsRequired();
        builder.Property(h => h.DecidedBy).HasMaxLength(255);
        builder.Property(h => h.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(h => h.UserModified).HasMaxLength(255);

        builder.ComplexProperty(h => h.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("Amount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("Currency").HasMaxLength(3);
        });

        builder.ComplexProperty(h => h.PreviousAmount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("PreviousAmount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("PreviousCurrency").HasMaxLength(3);
        });

        builder.ComplexProperty(h => h.NewAmount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("NewAmount").HasPrecision(19, 4);
            money.Property(m => m.Currency).HasColumnName("NewCurrency").HasMaxLength(3);
        });

        builder.Property(h => h.ChangeReason).HasMaxLength(1000);

        builder.HasIndex(h => new { h.ReserveComponentId, h.ChangeSequence }).IsUnique();
    }
}
