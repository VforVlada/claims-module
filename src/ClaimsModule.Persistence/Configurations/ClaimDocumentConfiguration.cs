using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClaimsModule.Persistence.Configurations;

public sealed class ClaimDocumentConfiguration : IEntityTypeConfiguration<ClaimDocument>
{
    public void Configure(EntityTypeBuilder<ClaimDocument> builder)
    {
        builder.ToTable("ClaimDocuments");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(500).IsRequired();
        builder.Property(d => d.SanitizedFileName).HasMaxLength(500).IsRequired();
        builder.Property(d => d.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(d => d.DocumentType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(d => d.BlobPath).HasMaxLength(1000).IsRequired();
        builder.Property(d => d.UploadedBy).HasMaxLength(255).IsRequired();
        builder.Property(d => d.UserCreated).HasMaxLength(255).IsRequired();
        builder.Property(d => d.UserModified).HasMaxLength(255);
    }
}
