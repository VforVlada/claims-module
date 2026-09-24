using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Claims.EventHandlers;

public sealed class ClaimStatusChangedEventHandler(IAuditLogService auditLog) : DomainEventHandler<ClaimStatusChangedEvent>
{
    public override Task Handle(ClaimStatusChangedEvent notification, CancellationToken cancellationToken) =>
        auditLog.LogAsync(notification.ClaimId, "STATUS_CHANGED", notification.OldStatus.ToString(), notification.NewStatus.ToString(), notification.ChangedBy, cancellationToken);
}
