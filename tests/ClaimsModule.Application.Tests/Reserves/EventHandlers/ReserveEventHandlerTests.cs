using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.EventHandlers;
using ClaimsModule.Application.Tests.TestHelpers;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.Events;
using ClaimsModule.Domain.ValueObjects;
using Moq;

namespace ClaimsModule.Application.Tests.Reserves.EventHandlers;

/// <summary>The "GL job enqueued" column of plan table 3.3, and JOB-01: exactly one job per balance-affecting change, with the right ids.</summary>
public class ReserveEventHandlerTests
{
    private readonly Mock<IAuditLogService> _auditLog = new();
    private readonly Mock<IBackgroundJobScheduler> _jobScheduler = new();
    private readonly FakeCurrentUserService _currentUser = new();

    [Theory]
    [InlineData(ApprovalStatus.AutoApproved, 1)]
    [InlineData(ApprovalStatus.PendingApproval, 0)]
    public async Task ReserveSubmitted_EnqueuesGlJobOnlyWhenAutoApproved(ApprovalStatus initialStatus, int expectedEnqueues)
    {
        var sut = new ReserveSubmittedEventHandler(_auditLog.Object, _jobScheduler.Object, _currentUser);
        var evt = new ReserveSubmittedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(5000m), initialStatus);

        await sut.Handle(evt, CancellationToken.None);

        _jobScheduler.Verify(j => j.EnqueuePostGlReserveChange(evt.ReserveHistoryId, evt.ClaimId, evt.ReserveComponentId), Times.Exactly(expectedEnqueues));
        _jobScheduler.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ReserveApproved_EnqueuesExactlyOneGlJobForThatHistoryRow()
    {
        var sut = new ReserveApprovedEventHandler(_auditLog.Object, _jobScheduler.Object, _currentUser);
        var evt = new ReserveApprovedEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new Money(50000m));

        await sut.Handle(evt, CancellationToken.None);

        _jobScheduler.Verify(j => j.EnqueuePostGlReserveChange(evt.ReserveHistoryId, evt.ClaimId, evt.ReserveComponentId), Times.Once);
        _jobScheduler.VerifyNoOtherCalls();
        _auditLog.Verify(a => a.LogAsync(evt.ClaimId, "RESERVE_APPROVED", null, It.IsAny<string?>(), _currentUser.UserName, It.IsAny<CancellationToken>(), null), Times.Once);
    }
}
