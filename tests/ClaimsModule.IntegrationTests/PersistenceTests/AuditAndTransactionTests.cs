using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Messaging;
using ClaimsModule.Domain.Common;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimsModule.IntegrationTests.PersistenceTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class AuditAndTransactionTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    /// <summary>I-DB-04: the SaveChanges interceptor stamps audit columns from the authenticated caller and the clock.</summary>
    [Fact]
    public async Task SaveThroughApi_SetsCreatedAndModifiedAuditColumns()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var created = await Factory.CreateHandlerClient().CreateClaimAsync();

        await using (var context = Factory.CreateDbContext())
        {
            var claim = await context.Claims.AsNoTracking().SingleAsync(c => c.Id == created.Id);
            Assert.Equal("Hannah Handler", claim.UserCreated);
            Assert.InRange(claim.CreatedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
            Assert.Null(claim.UpdatedAt);
        }

        (await Factory.CreateSupervisorClient().TransitionAsync(created.Id, ClaimStatus.Open)).EnsureSuccessStatusCode();

        await using (var context = Factory.CreateDbContext())
        {
            var claim = await context.Claims.AsNoTracking().SingleAsync(c => c.Id == created.Id);
            Assert.Equal("Sam Supervisor", claim.UserModified);
            Assert.NotNull(claim.UpdatedAt);
            Assert.True(claim.UpdatedAt >= claim.CreatedAt);
        }
    }

    public sealed record WriteThenThrowCommand(Guid ClaimId) : IRequest<Unit>, ICommand;

    public sealed class WriteThenThrowHandler(IApplicationDbContext context) : IRequestHandler<WriteThenThrowCommand, Unit>
    {
        public async Task<Unit> Handle(WriteThenThrowCommand request, CancellationToken cancellationToken)
        {
            var claim = new ClaimBuilder().BuildEntity();
            ((BaseEntity)claim).Id = request.ClaimId;
            context.Claims.Add(claim);
            await context.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Simulated failure after the first write.");
        }
    }

    /// <summary>I-DB-09 / ARCH-05: the unit-of-work transaction rolls back a write that was already flushed to SQL.</summary>
    [Fact]
    public async Task HandlerThrowsAfterItsFirstWrite_NothingIsPersisted()
    {
        using var failingApp = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddTransient<IRequestHandler<WriteThenThrowCommand, Unit>, WriteThenThrowHandler>()));
        var claimId = Guid.NewGuid();

        using (var scope = failingApp.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(new WriteThenThrowCommand(claimId)));
        }

        await using var context = Factory.CreateDbContext();
        Assert.False(await context.Claims.IgnoreQueryFilters().AnyAsync(c => c.Id == claimId));
    }

    /// <summary>I-DB-10: the audit trail is append-only — no service method mutates it, and the context refuses to.</summary>
    [Fact]
    public void AuditLogService_ExposesNoUpdateOrDeleteOperation()
    {
        var methods = typeof(IAuditLogService).GetMethods().Select(m => m.Name).ToList();

        Assert.Equal(["LogAsync"], methods);
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public async Task ModifyingOrDeletingAnAuditRow_IsRejected(EntityState attempted)
    {
        var created = await Factory.CreateHandlerClient().CreateClaimAsync();
        await using var context = Factory.CreateDbContext();
        var row = await context.ClaimAuditLogs.FirstAsync(a => a.ClaimId == created.Id);

        if (attempted == EntityState.Modified)
        {
            row.NewValues = "tampered";
        }
        else
        {
            context.ClaimAuditLogs.Remove(row);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
        await using var verify = Factory.CreateDbContext();
        Assert.NotEqual("tampered", (await verify.ClaimAuditLogs.AsNoTracking().SingleAsync(a => a.Id == row.Id)).NewValues);
    }

    private sealed class FailingAuditLogService : IAuditLogService
    {
        public Task LogAsync(Guid claimId, string action, string? oldValues, string? newValues, string performedBy, CancellationToken cancellationToken, string? idempotencyKey = null) =>
            throw new InvalidOperationException("Simulated audit write failure.");
    }

    /// <summary>
    /// ARCH-05: domain events are dispatched inside the command's transaction, so a failing audit
    /// write (the ClaimCreated handler) rolls the claim back too — no claim ever exists without
    /// its CLAIM_CREATED entry.
    /// </summary>
    [Fact]
    public async Task DomainEventHandlerFails_CommandIsRolledBack()
    {
        using var failingApp = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddScoped<IAuditLogService, FailingAuditLogService>()));
        var client = failingApp.CreateClient();
        client.DefaultRequestHeaders.Authorization = Factory.CreateHandlerClient().DefaultRequestHeaders.Authorization;
        await using var context = Factory.CreateDbContext();
        var claimsBefore = await context.Claims.IgnoreQueryFilters().CountAsync();

        var response = await client.PostAsJsonAsync("/api/claims", new ClaimBuilder().BuildCommand());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(claimsBefore, await context.Claims.IgnoreQueryFilters().CountAsync());
    }
}
