namespace ClaimsModule.Application.Common.Models;

public sealed record ValidationIssue(string Code, string Message, string? Field = null);
