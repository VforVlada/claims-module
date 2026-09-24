namespace ClaimsModule.Domain.Exceptions;

public sealed class InvalidReserveOperationException(string message) : DomainException(message);
