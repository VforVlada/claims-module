namespace ClaimsModule.API.Configuration;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>Defaults to the Angular dev server so `dotnet run` works out of the box without config.</summary>
    public string[] AllowedOrigins { get; set; } = ["http://localhost:4200"];
}
