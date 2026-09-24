using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Reserves.EventHandlers;

public sealed class ReserveApprovedEventHandler(
    IAuditLogService auditLog,
    IBackgroundJobScheduler jobScheduler,
    ICurrentUserService currentUser) : DomainEventHandler<ReserveApprovedEvent>
{
    public override async Task Handle(ReserveApprovedEvent notification, CancellationToken cancellationToken)
    {
        await auditLog.LogAsync(notification.ClaimId, "RESERVE_APPROVED", null, notification.Amount.ToString(), currentUser.UserName, cancellationToken);
        jobScheduler.EnqueuePostGlReserveChange(notification.ReserveHistoryId, notification.ClaimId, notification.ReserveComponentId);
    }
}
