using System.Net.Http.Json;
using ClaimsModule.Application.Claims.Commands;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.Enums;
using ClaimsModule.Domain.ValueObjects;
using ClaimsModule.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.IntegrationTests.Infrastructure;

/// <summary>Seeded reference data ids (mirrors ClaimsModule.Persistence.Seed, which is internal).</summary>
public static class Seeded
{
    public static readonly Guid CollisionCode = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    public static readonly Guid FireCode = Guid.Parse("00000000-0000-0000-0000-0000000000c4");
    public static readonly Guid InactiveCode = Guid.Parse("00000000-0000-0000-0000-0000000000ca");
    public static readonly Guid OrgBCode = Guid.Parse("00000000-0000-0000-0000-0000000000cb");

    /// <summary>In force 2026-01-01 → 2027-01-01.</summary>
    public static readonly Guid ActivePolicy = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    /// <summary>Expired 2025-03-01.</summary>
    public static readonly Guid ExpiredPolicy = Guid.Parse("00000000-0000-0000-0000-0000000000a3");

    /// <summary>In force 2026-09-01 00:00 → 2026-09-07 23:59:59 only.</summary>
    public static readonly Guid NarrowWindowPolicy = Guid.Parse("00000000-0000-0000-0000-0000000000a4");
}

/// <summary>A fixed caller, for building a ClaimsDbContext that sees exactly one tenant.</summary>
public sealed class TestCurrentUser(Guid organizationId, string userName = "integration-test", params string[] roles) : ICurrentUserService
{
    public string UserId => userName;

    public string UserName => userName;

    public Guid OrganizationEntityId => organizationId;

    public IReadOnlyCollection<string> Roles => roles;

    public bool IsInRole(string role) => roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}

public sealed class TestClock(DateTimeOffset? fixedNow = null) : IDateTimeProvider
{
    public DateTimeOffset UtcNow => fixedNow ?? DateTimeOffset.UtcNow;
}

public static class FactoryExtensions
{
    /// <summary>
    /// A real ClaimsDbContext scoped to one organization. DI-resolved contexts outside a request
    /// have no current user, so the tenant query filter (correctly) hides every row from them.
    /// </summary>
    public static ClaimsDbContext CreateDbContext(this ApiWebApplicationFactory factory, Guid? organizationId = null, DateTimeOffset? now = null)
    {
        var options = new DbContextOptionsBuilder<ClaimsDbContext>().UseSqlServer(factory.ConnectionString).Options;
        return new ClaimsDbContext(options, new TestCurrentUser(organizationId ?? ApiWebApplicationFactory.DefaultOrganizationId), new TestClock(now));
    }

    public static async Task<Claim> SeedClaimAsync(this ApiWebApplicationFactory factory, ClaimBuilder builder)
    {
        await using var context = factory.CreateDbContext(builder.OrganizationId);
        var claim = builder.BuildEntity();
        context.Claims.Add(claim);
        await context.SaveChangesAsync();
        return claim;
    }
}

/// <summary>Builds a claim either as an API command or directly as a domain entity (plan section 2: shared builders).</summary>
public sealed class ClaimBuilder
{
    private static int _sequence = 100_000;

    private Guid? _policyId;
    private DateTimeOffset _lossDate = DateTimeOffset.UtcNow.AddDays(-1);
    private Guid _causeOfLossCodeId = Seeded.CollisionCode;
    private string _assignedHandler = "Hannah Handler";
    private readonly List<ClaimPartyInput> _parties = [new(PartyType.Individual, PartyRole.Claimant, "John Doe", "john@example.com", null)];
    private readonly List<ClaimRiskObjectInput> _riskObjects = [];
    private InitialReserveInput? _initialReserve;
    private ClaimStatus _status = ClaimStatus.Draft;

    public Guid OrganizationId { get; private set; } = ApiWebApplicationFactory.DefaultOrganizationId;

    public ClaimBuilder ForOrganization(Guid organizationId) { OrganizationId = organizationId; return this; }

    public ClaimBuilder WithPolicy(Guid policyId) { _policyId = policyId; return this; }

    public ClaimBuilder WithLossDate(DateTimeOffset lossDate) { _lossDate = lossDate; return this; }

    public ClaimBuilder WithCauseOfLoss(Guid codeId) { _causeOfLossCodeId = codeId; return this; }

    public ClaimBuilder AssignedTo(string handler) { _assignedHandler = handler; return this; }

    public ClaimBuilder WithParties(params ClaimPartyInput[] parties) { _parties.Clear(); _parties.AddRange(parties); return this; }

    public ClaimBuilder WithRiskObject(AssetType type, string description, string? identifier = null)
    {
        _riskObjects.Add(new ClaimRiskObjectInput(type, description, identifier));
        return this;
    }

    public ClaimBuilder WithInitialReserve(decimal amount, ReserveComponentType type = ReserveComponentType.IndemnityReserve)
    {
        _initialReserve = new InitialReserveInput(type, amount);
        return this;
    }

    /// <summary>Entity builds only: walks the claim through the workflow to this status.</summary>
    public ClaimBuilder InStatus(ClaimStatus status) { _status = status; return this; }

    public CreateClaimCommand BuildCommand() => new(
        _policyId, ClaimType.Auto, _lossDate, "Rear-end collision at a junction", "New York, NY",
        _causeOfLossCodeId, _assignedHandler, [.. _parties], [.. _riskObjects], _initialReserve);

    public Claim BuildEntity()
    {
        var claim = Claim.Create(OrganizationId, ClaimNumber.Create(2026, Interlocked.Increment(ref _sequence)), _policyId,
            ClaimType.Auto, _assignedHandler, _lossDate, "Seeded claim", "New York, NY", _causeOfLossCodeId, "seed");
        foreach (var party in _parties)
        {
            claim.AddParty(party.PartyType, party.PartyRole, party.Name, party.ContactEmail, party.ContactPhone, "seed");
        }

        foreach (var riskObject in _riskObjects)
        {
            claim.AddRiskObject(riskObject.AssetType, riskObject.Description, riskObject.Identifier);
        }

        ClaimStatus[] path = _status switch
        {
            ClaimStatus.Draft => [],
            ClaimStatus.Open => [ClaimStatus.Open],
            ClaimStatus.UnderInvestigation => [ClaimStatus.Open, ClaimStatus.UnderInvestigation],
            ClaimStatus.PendingPayment => [ClaimStatus.Open, ClaimStatus.PendingPayment],
            ClaimStatus.Closed => [ClaimStatus.Open, ClaimStatus.Closed],
            ClaimStatus.Withdrawn => [ClaimStatus.Withdrawn],
            ClaimStatus.Reopened => [ClaimStatus.Open, ClaimStatus.Closed, ClaimStatus.Reopened],
            _ => throw new ArgumentOutOfRangeException()
        };
        foreach (var status in path)
        {
            claim.TransitionTo(status, "seed");
        }

        return claim;
    }
}

public sealed class ReserveBuilder
{
    private ReserveComponentType _type = ReserveComponentType.IndemnityReserve;
    private decimal _amount = 5000m;
    private bool _managerOverride;

    public ReserveBuilder OfType(ReserveComponentType type) { _type = type; return this; }

    public ReserveBuilder WithAmount(decimal amount) { _amount = amount; return this; }

    public ReserveBuilder WithManagerOverride() { _managerOverride = true; return this; }

    public object BuildRequest() => new { ComponentType = _type, Amount = _amount, Currency = "USD", ManagerOverrideConfirmed = _managerOverride };
}

public sealed record ResultEnvelope<T>(T Value, List<WarningDto> Warnings);

public sealed record WarningDto(string Code, string Message, string? Field);

public sealed record PagedResponse<T>(List<T> Items, int TotalCount, int PageNumber, int PageSize);

/// <summary>Thin HTTP helpers so tests read as scenarios, not plumbing.</summary>
public static class ApiClientExtensions
{
    public static async Task<ClaimDetailDto> CreateClaimAsync(this HttpClient client, ClaimBuilder? builder = null)
    {
        var response = await client.PostAsJsonAsync("/api/claims", (builder ?? new ClaimBuilder()).BuildCommand());
        await EnsureSuccessWithBodyAsync(response);
        return (await response.Content.ReadFromJsonAsync<ResultEnvelope<ClaimDetailDto>>())!.Value;
    }

    public static async Task<ReserveComponentDto> OpenReserveAsync(this HttpClient client, Guid claimId, ReserveBuilder builder)
    {
        var response = await client.PostAsJsonAsync($"/api/claims/{claimId}/reserves", builder.BuildRequest());
        await EnsureSuccessWithBodyAsync(response);
        return (await response.Content.ReadFromJsonAsync<ResultEnvelope<ReserveComponentDto>>())!.Value;
    }

    public static Task<HttpResponseMessage> ApproveAsync(this HttpClient client, ReserveComponentDto reserve, Guid historyId) =>
        client.PostAsJsonAsync($"/api/claims/{reserve.ClaimId}/reserves/{reserve.Id}/approve", new { ReserveHistoryId = historyId });

    public static Task<HttpResponseMessage> TransitionAsync(this HttpClient client, Guid claimId, ClaimStatus status) =>
        client.PutAsJsonAsync($"/api/claims/{claimId}/status", new { NewStatus = status });

    public static async Task<List<ClaimAuditLogDto>> GetAuditAsync(this HttpClient client, Guid claimId)
    {
        var page = await client.GetFromJsonAsync<PagedResponse<ClaimAuditLogDto>>($"/api/claims/{claimId}/audit?pageSize=200");
        return page!.Items;
    }

    public static async Task EnsureSuccessWithBodyAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{(int)response.StatusCode} {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }
}
