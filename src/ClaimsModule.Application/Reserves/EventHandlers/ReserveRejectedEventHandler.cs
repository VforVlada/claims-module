using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Reserves.EventHandlers;

public sealed class ReserveRejectedEventHandler(IAuditLogService auditLog, ICurrentUserService currentUser)
    : DomainEventHandler<ReserveRejectedEvent>
{
    public override Task Handle(ReserveRejectedEvent notification, CancellationToken cancellationToken) =>
        auditLog.LogAsync(notification.ClaimId, "RESERVE_REJECTED", notification.Reason, null, currentUser.UserName, cancellationToken);
}
