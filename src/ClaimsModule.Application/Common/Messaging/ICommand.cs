namespace ClaimsModule.Application.Common.Messaging;

/// <summary>
/// Marker for write-side requests. UnitOfWorkBehavior and DomainEventDispatchBehavior
/// only engage for requests implementing this — queries pass straight through.
/// </summary>
public interface ICommand;

/// <summary>Marker for read-side requests; purely documentational.</summary>
public interface IQuery;
