namespace ClaimsModule.Application.Policies.Dtos;

/// <summary>
/// A simulated policy's status on a given day, derived from its effective period. The real policy
/// admin system would own this; here there are no cancellations, so the dates decide it.
/// </summary>
public enum PolicyStatus
{
    Active = 0,
    Expired = 1,
    NotYetEffective = 2
}
