using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Entities;

public sealed class ClaimDocument : BaseEntity
{
    public Guid ClaimId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SanitizedFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public DocumentType DocumentType { get; set; } = DocumentType.Other;

    public long SizeBytes { get; set; }

    /// <summary>Path inside the "claim-documents" container: {organizationId}/{claimId}/{uniqueId}-{sanitizedFilename}.</summary>
    public string BlobPath { get; set; } = string.Empty;

    public string UploadedBy { get; set; } = string.Empty;
}
