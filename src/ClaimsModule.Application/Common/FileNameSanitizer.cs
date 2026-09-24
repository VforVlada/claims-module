using System.Text.RegularExpressions;

namespace ClaimsModule.Application.Common;

public static partial class FileNameSanitizer
{
    public static string Sanitize(string fileName)
    {
        var name = Path.GetFileName(fileName);
        name = UnsafeCharsRegex().Replace(name, "_");
        return string.IsNullOrWhiteSpace(name) ? "unnamed" : name;
    }

    [GeneratedRegex(@"[^a-zA-Z0-9._-]")]
    private static partial Regex UnsafeCharsRegex();
}
