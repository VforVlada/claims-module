using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Events;

public sealed record ClaimStatusChangedEvent(Guid ClaimId, ClaimStatus OldStatus, ClaimStatus NewStatus, string ChangedBy) : DomainEvent;
