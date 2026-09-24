using ClaimsModule.Domain.Common;

namespace ClaimsModule.Domain.Events;

/// <summary>A non-blocking business-rule warning (e.g. BR-C-02) recorded against a claim.</summary>
public sealed record ClaimWarningRaisedEvent(Guid ClaimId, string Code, string Message, string RaisedBy) : DomainEvent;
