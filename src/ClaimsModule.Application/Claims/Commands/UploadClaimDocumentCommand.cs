using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Enums;
using MediatR;

namespace ClaimsModule.Application.Claims.Commands;

public sealed record UploadClaimDocumentCommand(
    Guid ClaimId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Stream Content,
    DocumentType DocumentType = DocumentType.Other) : IRequest<ClaimDocumentDto>, ICommand;
