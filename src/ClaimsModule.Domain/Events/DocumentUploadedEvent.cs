using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

public sealed record DocumentUploadedEvent(Guid ClaimId, Guid DocumentId, string FileName, string UploadedBy) : DomainEvent;
