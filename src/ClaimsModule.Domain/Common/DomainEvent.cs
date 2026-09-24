namespace ClaimsModule.Domain.Common;

/// <summary>
/// Plain record — no MediatR here (architecture rule A-01). The Application layer wraps each
/// event in DomainEventNotification&lt;T&gt; to publish it.
/// </summary>
public abstract record DomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
