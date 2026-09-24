using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Events;

public sealed record ReserveSubmittedEvent(
    Guid ReserveComponentId,
    Guid ClaimId,
    Guid ReserveHistoryId,
    Money Amount,
    ApprovalStatus InitialStatus) : DomainEvent;
