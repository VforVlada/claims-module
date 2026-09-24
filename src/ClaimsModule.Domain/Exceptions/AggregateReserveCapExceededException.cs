namespace ClaimsModule.Domain.Exceptions;

/// <summary>BR-R-07: a claim's aggregate approved reserve total may not exceed $10,000,000 without manager override.</summary>
public sealed class AggregateReserveCapExceededException(string message) : DomainException(message);
