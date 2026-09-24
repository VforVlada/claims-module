using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Claims.EventHandlers;

public sealed class ClaimCreatedEventHandler(IAuditLogService auditLog) : DomainEventHandler<ClaimCreatedEvent>
{
    public override Task Handle(ClaimCreatedEvent notification, CancellationToken cancellationToken) =>
        auditLog.LogAsync(notification.ClaimId, "CLAIM_CREATED", null, notification.ClaimNumber.Value, notification.CreatedBy, cancellationToken);
}
