using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Queries;

public sealed class GetClaimDocumentsQueryHandler(IApplicationDbContext context, IStorageService storageService, IMapper mapper)
    : IRequestHandler<GetClaimDocumentsQuery, IReadOnlyCollection<ClaimDocumentDto>>
{
    private static readonly TimeSpan DownloadUrlTtl = TimeSpan.FromHours(1);

    public async Task<IReadOnlyCollection<ClaimDocumentDto>> Handle(GetClaimDocumentsQuery request, CancellationToken cancellationToken)
    {
        var documents = await context.ClaimDocuments.AsNoTracking()
            .Where(d => d.ClaimId == request.ClaimId)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<ClaimDocumentDto>(documents.Count);
        foreach (var document in documents)
        {
            var downloadUrl = await storageService.GetDownloadUrlAsync(document.BlobPath, DownloadUrlTtl, cancellationToken);
            var dto = mapper.Map<ClaimDocumentDto>(document);
            result.Add(new ClaimDocumentDto
            {
                Id = dto.Id,
                FileName = dto.FileName,
                ContentType = dto.ContentType,
                DocumentType = dto.DocumentType,
                SizeBytes = dto.SizeBytes,
                UploadedBy = dto.UploadedBy,
                CreatedAt = dto.CreatedAt,
                DownloadUrl = downloadUrl
            });
        }

        return result;
    }
}
