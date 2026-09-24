using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Claims.EventHandlers;

public sealed class DocumentUploadedEventHandler(IAuditLogService auditLog) : DomainEventHandler<DocumentUploadedEvent>
{
    public override Task Handle(DocumentUploadedEvent notification, CancellationToken cancellationToken) =>
        auditLog.LogAsync(notification.ClaimId, "DOCUMENT_UPLOADED", null, notification.FileName, notification.UploadedBy, cancellationToken);
}
