namespace ClaimsModule.Domain.Common;

public abstract class AggregateRoot : BaseEntity, IHasDomainEvents
{
    private readonly List<DomainEvent> _domainEvents = [];

    public byte[]? RowVersion { get; set; }

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
