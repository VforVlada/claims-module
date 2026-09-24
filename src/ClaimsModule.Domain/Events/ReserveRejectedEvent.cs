using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

public sealed record ReserveRejectedEvent(Guid ReserveComponentId, Guid ClaimId, Guid ReserveHistoryId, string Reason) : DomainEvent;
