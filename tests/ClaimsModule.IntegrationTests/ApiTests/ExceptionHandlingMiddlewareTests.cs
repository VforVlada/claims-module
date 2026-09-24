using ClaimsModule.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaimsModule.IntegrationTests.ApiTests;

/// <summary>
/// I-DB-08's "API returns 409" half (the persistence half — proving RowVersion actually
/// triggers DbUpdateConcurrencyException under a real race — is ConcurrencyTests). Exercises
/// the middleware directly rather than through Testcontainers: deterministic, and doesn't need
/// a real concurrent-write race just to prove the status-code mapping is correct.
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_DbUpdateConcurrencyException_Returns409()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new DbUpdateConcurrencyException("stale row version"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(409, context.Response.StatusCode);
    }
}
