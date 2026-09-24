namespace ClaimsModule.Application.Common;

public static class DocumentPolicy
{
    public const long MaxSizeBytes = 50 * 1024 * 1024;

    public static readonly IReadOnlyCollection<string> AllowedContentTypes =
    [
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv"
    ];
}
