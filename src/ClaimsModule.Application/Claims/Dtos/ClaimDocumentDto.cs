using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Application.Claims.Dtos;

public sealed class ClaimDocumentDto
{
    public Guid Id { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string ContentType { get; init; } = string.Empty;

    public DocumentType DocumentType { get; init; }

    public long SizeBytes { get; init; }

    public string UploadedBy { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>SAS URL, 1h TTL — populated by the query handler, not the mapper.</summary>
    public string DownloadUrl { get; init; } = string.Empty;
}
