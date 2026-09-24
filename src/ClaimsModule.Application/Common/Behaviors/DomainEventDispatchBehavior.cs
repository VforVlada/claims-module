using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using MediatR;

namespace ClaimsModule.Application.Common.Behaviors;

/// <summary>
/// Dispatches domain events raised during a command AFTER the transaction has committed
/// (this behavior is registered outside UnitOfWorkBehavior in the pipeline, so its
/// post-next() code runs once SaveChanges/commit has already happened).
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

        var entitiesWithEvents = context.GetEntitiesWithDomainEvents();
        var domainEvents = entitiesWithEvents.SelectMany(e => e.DomainEvents).ToList();

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var domainEvent in domainEvents)
        {
            await publisher.Publish(DomainEventNotification.For(domainEvent), cancellationToken);
        }

        return response;
    }
}
