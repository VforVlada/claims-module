using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Claims.EventHandlers;

public sealed class ClaimWarningRaisedEventHandler(IAuditLogService auditLog) : DomainEventHandler<ClaimWarningRaisedEvent>
{
    public override Task Handle(ClaimWarningRaisedEvent notification, CancellationToken cancellationToken) =>
        auditLog.LogAsync(notification.ClaimId, "CLAIM_WARNING", null, $"{notification.Code}: {notification.Message}", notification.RaisedBy, cancellationToken);
}
