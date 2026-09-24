namespace ClaimsModule.Application.Common.Exceptions;

/// <summary>
/// The message is shown to users as-is (it becomes the 404 ProblemDetails title), so it names the
/// record in plain words and leaves out the key; the key stays on the exception for logging.
/// </summary>
public sealed class NotFoundException(string entityName, object key)
    : Exception($"The {Describe(entityName)} could not be found. It may have been removed, or you may not have access to it.")
{
    public string EntityName { get; } = entityName;

    public object Key { get; } = key;

    private static string Describe(string entityName) => entityName switch
    {
        nameof(Domain.Entities.Claim) => "claim",
        nameof(Domain.Entities.ClaimReserveComponent) => "reserve",
        nameof(Domain.Entities.Policy) => "policy",
        _ => "record"
    };
}
