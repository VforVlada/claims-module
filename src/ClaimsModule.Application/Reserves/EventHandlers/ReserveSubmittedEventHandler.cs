using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;

namespace ClaimsModule.Application.Reserves.EventHandlers;

public sealed class ReserveSubmittedEventHandler(
    IAuditLogService auditLog,
    IBackgroundJobScheduler jobScheduler,
    ICurrentUserService currentUser) : DomainEventHandler<ReserveSubmittedEvent>
{
    public override async Task Handle(ReserveSubmittedEvent notification, CancellationToken cancellationToken)
    {
        var action = notification.InitialStatus == ApprovalStatus.AutoApproved ? "RESERVE_AUTO_APPROVED" : "RESERVE_CREATED";
        await auditLog.LogAsync(notification.ClaimId, action, null, notification.Amount.ToString(), currentUser.UserName, cancellationToken);

        if (notification.InitialStatus == ApprovalStatus.AutoApproved)
        {
            jobScheduler.EnqueuePostGlReserveChange(notification.ReserveHistoryId, notification.ClaimId, notification.ReserveComponentId);
        }
    }
}
