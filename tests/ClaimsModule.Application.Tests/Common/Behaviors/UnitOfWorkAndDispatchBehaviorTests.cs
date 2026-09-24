using ClaimsModule.Application.Common.Behaviors;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Common;
using MediatR;
using Moq;

namespace ClaimsModule.Application.Tests.Common.Behaviors;

public class UnitOfWorkBehaviorTests
{
    public sealed record TestCommand : IRequest<string>, ICommand;

    public sealed record TestQuery : IRequest<string>, IQuery;

    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    /// <summary>ARCH-05: a command commits exactly once, after its handler finishes.</summary>
    [Fact]
    public async Task Handle_CommandSucceeds_BeginsAndCommitsOnce()
    {
        var sut = new UnitOfWorkBehavior<TestCommand, string>(_unitOfWork.Object);

        var result = await sut.Handle(new TestCommand(), _ => Task.FromResult("ok"), CancellationToken.None);

        Assert.Equal("ok", result);
        _unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>ARCH-05: a handler that throws after writing rolls back and never commits.</summary>
    [Fact]
    public async Task Handle_HandlerThrows_RollsBackAndDoesNotCommit()
    {
        var sut = new UnitOfWorkBehavior<TestCommand, string>(_unitOfWork.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(new TestCommand(), _ => throw new InvalidOperationException("boom"), CancellationToken.None));

        _unitOfWork.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Query_OpensNoTransaction()
    {
        var sut = new UnitOfWorkBehavior<TestQuery, string>(_unitOfWork.Object);

        await sut.Handle(new TestQuery(), _ => Task.FromResult("ok"), CancellationToken.None);

        _unitOfWork.VerifyNoOtherCalls();
    }
}

public class DomainEventDispatchBehaviorTests
{
    private sealed record TestEvent : DomainEvent;

    private sealed class TestAggregate : AggregateRoot
    {
        public void Raise() => AddDomainEvent(new TestEvent());
    }

    /// <summary>Events publish only after the handler has run (and saved), and are cleared so they can't publish twice.</summary>
    [Fact]
    public async Task Handle_CommandSucceeds_PublishesEventsAfterNextAndClearsThem()
    {
        var aggregate = new TestAggregate();
        var context = new Mock<IApplicationDbContext>();
        context.Setup(c => c.GetEntitiesWithDomainEvents()).Returns(() => aggregate.DomainEvents.Count == 0 ? [] : [aggregate]);
        var publisher = new Mock<IPublisher>();
        var order = new List<string>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("publish"))
            .Returns(Task.CompletedTask);
        var sut = new DomainEventDispatchBehavior<UnitOfWorkBehaviorTests.TestCommand, string>(context.Object, publisher.Object);

        await sut.Handle(new UnitOfWorkBehaviorTests.TestCommand(), _ =>
        {
            aggregate.Raise();
            order.Add("next");
            return Task.FromResult("ok");
        }, CancellationToken.None);

        Assert.Equal(["next", "publish"], order);
        Assert.Empty(aggregate.DomainEvents);
    }

    /// <summary>ARCH-05: dispatch is registered inside the unit of work, so event handlers' writes share the command's transaction.</summary>
    [Fact]
    public void Pipeline_DispatchesEventsInsideTheUnitOfWork()
    {
        var behaviors = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddApplication()
            .Where(d => d.ServiceType == typeof(IPipelineBehavior<,>))
            .Select(d => d.ImplementationType)
            .ToList();

        Assert.True(behaviors.IndexOf(typeof(UnitOfWorkBehavior<,>)) < behaviors.IndexOf(typeof(DomainEventDispatchBehavior<,>)));
    }

    /// <summary>JOB-04: when the inner pipeline throws (rolled-back transaction), nothing is published — so no GL job can be enqueued.</summary>
    [Fact]
    public async Task Handle_InnerPipelineThrows_PublishesNothing()
    {
        var context = new Mock<IApplicationDbContext>();
        var publisher = new Mock<IPublisher>();
        var sut = new DomainEventDispatchBehavior<UnitOfWorkBehaviorTests.TestCommand, string>(context.Object, publisher.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.Handle(new UnitOfWorkBehaviorTests.TestCommand(), _ => throw new InvalidOperationException("rolled back"), CancellationToken.None));

        publisher.VerifyNoOtherCalls();
    }
}
