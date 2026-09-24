using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;

namespace ClaimsModule.Domain.Events;

public sealed record PartyAddedEvent(Guid ClaimId, Guid PartyId, PartyRole PartyRole, string AddedBy) : DomainEvent;
