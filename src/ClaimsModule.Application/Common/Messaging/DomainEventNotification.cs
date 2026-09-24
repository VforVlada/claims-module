using ClaimsModule.Domain.Common;
using MediatR;

namespace ClaimsModule.Application.Common.Messaging;

/// <summary>
/// Carries a domain event through MediatR. Domain events stay plain records so the Domain
/// project has no dependency on MediatR (architecture rule A-01); this wrapper is the only
/// place they meet.
/// </summary>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
    where TEvent : DomainEvent;

public static class DomainEventNotification
{
    /// <summary>Wraps an event in DomainEventNotification&lt;TRuntimeType&gt;, so MediatR resolves handlers by the concrete event type.</summary>
    public static INotification For(DomainEvent domainEvent) =>
        (INotification)Activator.CreateInstance(typeof(DomainEventNotification<>).MakeGenericType(domainEvent.GetType()), domainEvent)!;
}

/// <summary>Base for domain event handlers: unwraps DomainEventNotification so handlers deal only in the event itself.</summary>
public abstract class DomainEventHandler<TEvent> : INotificationHandler<DomainEventNotification<TEvent>>
    where TEvent : DomainEvent
{
    public Task Handle(DomainEventNotification<TEvent> notification, CancellationToken cancellationToken) =>
        Handle(notification.DomainEvent, cancellationToken);

    public abstract Task Handle(TEvent notification, CancellationToken cancellationToken);
}
