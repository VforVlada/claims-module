using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Events;

public sealed record ReserveApprovedEvent(Guid ReserveComponentId, Guid ClaimId, Guid ReserveHistoryId, Money Amount) : DomainEvent;
