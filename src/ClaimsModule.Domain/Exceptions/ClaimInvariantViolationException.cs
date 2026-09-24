namespace ClaimsModule.Domain.Exceptions;

public sealed class ClaimInvariantViolationException(string message) : DomainException(message);
