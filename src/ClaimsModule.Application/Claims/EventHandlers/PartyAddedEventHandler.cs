using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Claims.EventHandlers;

public sealed class PartyAddedEventHandler(IAuditLogService auditLog) : DomainEventHandler<PartyAddedEvent>
{
    public override Task Handle(PartyAddedEvent notification, CancellationToken cancellationToken) =>
        auditLog.LogAsync(notification.ClaimId, "PARTY_ADDED", null, notification.PartyRole.ToString(), notification.AddedBy, cancellationToken);
}
