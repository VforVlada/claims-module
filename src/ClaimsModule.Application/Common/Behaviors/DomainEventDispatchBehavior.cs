using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Dispatches domain events raised during a command once its handler has saved, but INSIDE the
/// command's transaction (this behavior is registered inside UnitOfWorkBehavior). Event handlers'
/// writes (audit rows) are therefore atomic with the change that raised the event: if one fails,
/// the whole command rolls back. Side effects outside the database (Hangfire enqueues) are
/// deferred to commit via IUnitOfWork.OnCommitted.
/// </summary>
public sealed class DomainEventDispatchBehavior<TRequest, TResponse>(
    IApplicationDbContext context,
    IPublisher publisher)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next();

        if (request is not ICommand)
        {
            return response;
        }

        // Loop: an event handler may cause further events to be raised.
        IReadOnlyCollection<IHasDomainEvents> entitiesWithEvents;
        while ((entitiesWithEvents = context.GetEntitiesWithDomainEvents()).Count != 0)
        {
            var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

            foreach (var entity in entitiesWithEvents)
            {
                entity.ClearDomainEvents();
            }

            foreach (var domainEvent in domainEvents)
            {
                await publisher.Publish(DomainEventNotification.For(domainEvent), cancellationToken);
            }
        }

        return response;
    }
}
