using ClaimsModule.API.Auth;
using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Claims.Queries;
using ClaimsModule.Application.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Infrastructure.Storage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace ClaimsModule.API.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.AnyRole)]
public sealed class DocumentsController(ISender mediator, IOptions<StorageSettings> storageOptions) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypeProvider = new();

    [HttpPost("api/claims/{claimId:guid}/documents")]
    [RequestSizeLimit(MaxRequestBytes)]
    public async Task<ActionResult<ClaimDocumentDto>> Upload(Guid claimId, IFormFile file, [FromForm] DocumentType documentType = DocumentType.Other, CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        var command = new UploadClaimDocumentCommand(claimId, file.FileName, file.ContentType, file.Length, stream, documentType);
        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpGet("api/claims/{claimId:guid}/documents")]
    public async Task<ActionResult<IReadOnlyCollection<ClaimDocumentDto>>> List(Guid claimId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetClaimDocumentsQuery(claimId), cancellationToken));

    /// <summary>Dev-only download endpoint backing LocalFileSystemStorageService's signed URLs.</summary>
    [HttpGet("api/claims/local-documents")]
    [AllowAnonymous]
    public IActionResult DownloadLocal([FromQuery] string path, [FromQuery] long exp, [FromQuery] string sig)
    {
        var settings = storageOptions.Value;
        if (!LocalDocumentUrlSigner.IsValid(settings.LocalFileSystemSigningSecret, path, exp, sig))
        {
            return Unauthorized();
        }

        var safeRelative = path.Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(AppContext.BaseDirectory, settings.LocalFileSystemRootPath, safeRelative);
        if (!System.IO.File.Exists(physicalPath))
        {
            return NotFound();
        }

        if (!ContentTypeProvider.TryGetContentType(physicalPath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        return PhysicalFile(physicalPath, contentType, Path.GetFileName(physicalPath));
    }

    // The file-size rule itself lives in UploadClaimDocumentCommandValidator (a clean 400 with a
    // field error). This transport cap only needs headroom for multipart framing on top of it.
    private const long MaxRequestBytes = DocumentPolicy.MaxSizeBytes + (1024 * 1024);
}
