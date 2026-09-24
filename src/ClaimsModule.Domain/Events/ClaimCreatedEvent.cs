using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.ValueObjects;

namespace ClaimsModule.Domain.Events;

public sealed record ClaimCreatedEvent(Guid ClaimId, ClaimNumber ClaimNumber, string CreatedBy) : DomainEvent;
