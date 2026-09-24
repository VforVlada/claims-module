using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Common;
using FluentValidation;

namespace ClaimsModule.Application.Claims.Validators;

public sealed class UploadClaimDocumentCommandValidator : AbstractValidator<UploadClaimDocumentCommand>
{
    public UploadClaimDocumentCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();
        RuleFor(c => c.FileName).NotEmpty();
        RuleFor(c => c.DocumentType).IsInEnum();

        RuleFor(c => c.ContentType)
            .Must(ct => DocumentPolicy.AllowedContentTypes.Contains(ct))
            .WithMessage($"Content type is not allowed. Allowed types: {string.Join(", ", DocumentPolicy.AllowedContentTypes)}.");

        RuleFor(c => c.SizeBytes)
            .GreaterThan(0)
            .LessThanOrEqualTo(DocumentPolicy.MaxSizeBytes)
            .WithMessage($"File size must be between 1 byte and {DocumentPolicy.MaxSizeBytes / (1024 * 1024)}MB.");
    }
}
