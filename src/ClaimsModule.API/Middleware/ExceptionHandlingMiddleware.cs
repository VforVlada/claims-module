using System.Net;
using System.Text.Json;
using ClaimsModule.Application.Common.Exceptions;
using ClaimsModule.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using ValidationException = ClaimsModule.Application.Common.Exceptions.ValidationException;

namespace ClaimsModule.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    /// <summary>
    /// Every error leaves as an RFC 7807 ProblemDetails body (application/problem+json). Status
    /// convention: 400 request validation (field errors in "errors"), 403 authority, 404 missing
    /// or other-tenant resource, 409 state conflicts (illegal status transition, stale
    /// rowversion), 422 other business-rule violations, 500 everything else — never with a
    /// stack trace in the body.
    /// </summary>
    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (status, type, title, extensions) = exception switch
        {
            ValidationException validation => (HttpStatusCode.BadRequest, "ValidationError",
                "One or more validation errors occurred.", new Dictionary<string, object?> { ["errors"] = validation.Errors }),
            InvalidClaimStatusTransitionException transition => (HttpStatusCode.Conflict, "InvalidStatusTransition",
                transition.Message, new Dictionary<string, object?> { ["allowedNextStatuses"] = transition.AllowedNextStatuses.Select(s => s.ToString()) }),
            NotFoundException notFound => (HttpStatusCode.NotFound, "NotFound", notFound.Message, null),
            ForbiddenAccessException forbidden => (HttpStatusCode.Forbidden, "Forbidden", forbidden.Message, null),
            DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "ConcurrencyConflict",
                "The record was modified by another request. Reload and try again.", null),
            DomainException domain => (HttpStatusCode.UnprocessableEntity, "BusinessRuleViolation", domain.Message, null),
            BadHttpRequestException badRequest => ((HttpStatusCode)badRequest.StatusCode, "BadRequest", badRequest.Message, null),
            _ => (HttpStatusCode.InternalServerError, "ServerError", "An unexpected error occurred.", (Dictionary<string, object?>?)null)
        };

        if (status == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Handled exception ({StatusCode}) for {Method} {Path}", (int)status, context.Request.Method, context.Request.Path);
        }

        var body = new Dictionary<string, object?>
        {
            ["type"] = type,
            ["title"] = title,
            ["status"] = (int)status,
            ["instance"] = context.Request.Path.Value,
            ["traceId"] = context.TraceIdentifier
        };
        foreach (var (key, value) in extensions ?? [])
        {
            body[key] = value;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
    }
}
