using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands;

public sealed class UploadClaimDocumentCommandHandler(
    IApplicationDbContext context,
    IStorageService storageService,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<UploadClaimDocumentCommand, ClaimDocumentDto>
{
    public async Task<ClaimDocumentDto> Handle(UploadClaimDocumentCommand request, CancellationToken cancellationToken)
    {
        var claim = await context.Claims.Include(c => c.Documents)
            .FirstOrDefaultAsync(c => c.Id == request.ClaimId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Claim), request.ClaimId);

        var sanitizedFileName = FileNameSanitizer.Sanitize(request.FileName);
        // Relative to the storage container (which is itself "claim-documents"). The unique
        // segment keeps two uploads with the same file name from colliding on one blob.
        var blobPath = $"{claim.OrganizationEntityId}/{claim.Id}/{Guid.NewGuid():N}-{sanitizedFileName}";

        await storageService.UploadAsync(blobPath, request.Content, request.ContentType, cancellationToken);

        var document = claim.AddDocument(request.FileName, sanitizedFileName, request.ContentType, request.SizeBytes, blobPath, currentUser.UserName, request.DocumentType);
        await context.SaveChangesAsync(cancellationToken);

        return mapper.Map<ClaimDocumentDto>(document);
    }
}
