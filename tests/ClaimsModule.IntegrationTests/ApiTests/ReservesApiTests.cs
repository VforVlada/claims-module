using System.Net;
using System.Net.Http.Json;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Enums;
using ClaimsModule.IntegrationTests.Infrastructure;

namespace ClaimsModule.IntegrationTests.ApiTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ReservesApiTests(ApiWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task<(ReserveComponentDto Reserve, Guid HistoryId)> OpenPendingReserveAsync(decimal amount)
    {
        var handler = Factory.CreateHandlerClient();
        var claim = await handler.CreateClaimAsync();
        var reserve = await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().WithAmount(amount));
        return (reserve, reserve.History.Single().Id);
    }

    /// <summary>I-API-10 / RES-13: the controller-level role gate rejects a Handler before any tier logic runs.</summary>
    [Fact]
    public async Task Approve_AsHandler_Returns403()
    {
        var (reserve, historyId) = await OpenPendingReserveAsync(50_000m);

        var response = await Factory.CreateHandlerClient().ApproveAsync(reserve, historyId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>I-API-11: a Supervisor approves a Supervisor-tier reserve → 200, balance moves, exactly one GL job enqueued.</summary>
    [Fact]
    public async Task Approve_SupervisorTierBySupervisor_Returns200AndEnqueuesOneGlJob()
    {
        var (reserve, historyId) = await OpenPendingReserveAsync(50_000m);
        Assert.Equal(0, await Factory.CountEnqueuedJobsMentioningAsync(historyId));

        var response = await Factory.CreateSupervisorClient().ApproveAsync(reserve, historyId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = (await response.Content.ReadFromJsonAsync<ReserveComponentDto>())!;
        Assert.Equal(50_000m, approved.CurrentAmount);
        Assert.Equal(ApprovalStatus.Approved, approved.History.Single().ApprovalStatus);
        Assert.Equal(1, await Factory.CountEnqueuedJobsMentioningAsync(historyId));
    }

    /// <summary>RES-05 over HTTP: a Supervisor can't approve a Manager-tier change (→ 403); a Manager can.</summary>
    [Fact]
    public async Task Approve_ManagerTier_ForbiddenForSupervisor_AllowedForManager()
    {
        var (reserve, historyId) = await OpenPendingReserveAsync(150_000m);

        Assert.Equal(HttpStatusCode.Forbidden, (await Factory.CreateSupervisorClient().ApproveAsync(reserve, historyId)).StatusCode);
        Assert.Equal(0, await Factory.CountEnqueuedJobsMentioningAsync(historyId));
        Assert.Equal(HttpStatusCode.OK, (await Factory.CreateManagerClient().ApproveAsync(reserve, historyId)).StatusCode);
    }

    /// <summary>An auto-approved reserve (≤ 10,000) posts to the GL without anyone approving it.</summary>
    [Fact]
    public async Task Open_AutoTier_EnqueuesGlJobImmediately()
    {
        var (reserve, historyId) = await OpenPendingReserveAsync(5_000m);

        Assert.Equal(ApprovalStatus.AutoApproved, reserve.History.Single().ApprovalStatus);
        Assert.Equal(1, await Factory.CountEnqueuedJobsMentioningAsync(historyId));
    }

    [Fact]
    public async Task Open_ZeroAmount_Returns400()
    {
        var claim = await Factory.CreateHandlerClient().CreateClaimAsync();

        var response = await Factory.CreateHandlerClient().PostAsJsonAsync($"/api/claims/{claim.Id}/reserves", new ReserveBuilder().WithAmount(0m).BuildRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reject_WithReason_ThenResubmit_KeepsBothRecords()
    {
        var (reserve, historyId) = await OpenPendingReserveAsync(50_000m);
        var supervisor = Factory.CreateSupervisorClient();

        var reject = await supervisor.PostAsJsonAsync($"/api/claims/{reserve.ClaimId}/reserves/{reserve.Id}/reject", new { ReserveHistoryId = historyId, Reason = "Needs a repair estimate" });
        reject.EnsureSuccessStatusCode();
        var resubmit = await Factory.CreateHandlerClient().PutAsJsonAsync($"/api/claims/{reserve.ClaimId}/reserves/{reserve.Id}", new { Amount = 40_000m, Reason = "Resubmitted with a repair estimate", Currency = "USD" });
        await ApiClientExtensions.EnsureSuccessWithBodyAsync(resubmit);

        var history = (await resubmit.Content.ReadFromJsonAsync<ResultEnvelope<ReserveComponentDto>>())!.Value.History;
        Assert.Equal(2, history.Count);
        Assert.Contains(history, h => h.ApprovalStatus == ApprovalStatus.Rejected && h.RejectionReason == "Needs a repair estimate");
        Assert.Contains(history, h => h.ApprovalStatus == ApprovalStatus.PendingApproval && h.ChangeSequence == 2);
    }

    [Fact]
    public async Task Reject_WithEmptyReason_Returns400()
    {
        var (reserve, historyId) = await OpenPendingReserveAsync(50_000m);

        var response = await Factory.CreateSupervisorClient().PostAsJsonAsync($"/api/claims/{reserve.ClaimId}/reserves/{reserve.Id}/reject", new { ReserveHistoryId = historyId, Reason = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>BR-R-07 at approval: crossing $10M on approval needs a Manager's explicit override, and is flagged and audited.</summary>
    [Fact]
    public async Task Approve_CrossingAggregateCap_RequiresManagerOverride()
    {
        var handler = Factory.CreateHandlerClient();
        var manager = Factory.CreateManagerClient();
        var claim = await handler.CreateClaimAsync();
        var big = await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().WithAmount(9_950_000m));
        (await manager.ApproveAsync(big, big.History.Single().Id)).EnsureSuccessStatusCode();
        var extra = await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().OfType(ReserveComponentType.ExpenseReserve).WithAmount(150_000m));
        var extraHistoryId = extra.History.Single().Id;
        var approveUrl = $"/api/claims/{claim.Id}/reserves/{extra.Id}/approve";

        var withoutOverride = await manager.PostAsJsonAsync(approveUrl, new { ReserveHistoryId = extraHistoryId });
        var withOverride = await manager.PostAsJsonAsync(approveUrl, new { ReserveHistoryId = extraHistoryId, ManagerOverrideConfirmed = true });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, withoutOverride.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withOverride.StatusCode);
        var detail = await manager.GetFromJsonAsync<Application.Claims.Dtos.ClaimDetailDto>($"/api/claims/{claim.Id}");
        Assert.True(detail!.RequiresManagerOverride);
        Assert.Contains(await manager.GetAuditAsync(claim.Id), a => a.Action == "CLAIM_WARNING" && a.NewValues!.StartsWith("BR-R-07"));
    }

    /// <summary>§3.2 / §3.3.3: adjusting sets a new amount; history keeps previous → new, the change, the reason and who made it.</summary>
    [Fact]
    public async Task Adjust_RecordsPreviousNewAmountReasonAndChangedBy()
    {
        var handler = Factory.CreateHandlerClient();
        var claim = await handler.CreateClaimAsync();
        var reserve = await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().WithAmount(5_000m));

        var response = await handler.PutAsJsonAsync($"/api/claims/{claim.Id}/reserves/{reserve.Id}", new { Amount = 8_000m, Reason = "Second repair quote was higher" });
        await ApiClientExtensions.EnsureSuccessWithBodyAsync(response);

        var adjusted = (await response.Content.ReadFromJsonAsync<ResultEnvelope<ReserveComponentDto>>())!.Value;
        var change = adjusted.History.Single(h => h.ChangeSequence == 2);
        Assert.Equal(8_000m, adjusted.CurrentAmount);
        Assert.Equal((5_000m, 8_000m, 3_000m), (change.PreviousAmount, change.NewAmount, change.Amount));
        Assert.Equal("Second repair quote was higher", change.ChangeReason);
        Assert.Equal("Hannah Handler", change.RequestedBy);
        Assert.Equal("Initial reserve", adjusted.History.Single(h => h.ChangeSequence == 1).ChangeReason);
    }

    [Theory]
    [InlineData(0, "Closing it out")]
    [InlineData(-100, "Negative")]
    [InlineData(7_000, "")]
    public async Task Adjust_InvalidAmountOrMissingReason_Returns400(decimal amount, string reason)
    {
        var handler = Factory.CreateHandlerClient();
        var claim = await handler.CreateClaimAsync();
        var reserve = await handler.OpenReserveAsync(claim.Id, new ReserveBuilder().WithAmount(5_000m));

        var response = await handler.PutAsJsonAsync($"/api/claims/{claim.Id}/reserves/{reserve.Id}", new { Amount = amount, Reason = reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>U-R-22 over HTTP: no new reserves on a closed claim (business rule → 422).</summary>
    [Fact]
    public async Task Open_OnClosedClaim_Returns422()
    {
        var claim = await Factory.SeedClaimAsync(new ClaimBuilder().InStatus(ClaimStatus.Closed));

        var response = await Factory.CreateHandlerClient().PostAsJsonAsync($"/api/claims/{claim.Id}/reserves", new ReserveBuilder().BuildRequest());

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    /// <summary>
    /// §3.3.3 list: each component reports its current amount, its approval status (the latest
    /// change's) and its history. Closing the claim closes its reserve lines, so a change left
    /// pending can no longer be approved (422) but can still be rejected.
    /// </summary>
    [Fact]
    public async Task List_ReportsApprovalStatusAndLineStatus_ClosedClaimBlocksApproval()
    {
        var (reserve, historyId) = await OpenPendingReserveAsync(50_000m);
        var supervisor = Factory.CreateSupervisorClient();
        async Task<ReserveComponentDto> ListSingle() =>
            Assert.Single((await supervisor.GetFromJsonAsync<List<ReserveComponentDto>>($"/api/claims/{reserve.ClaimId}/reserves"))!);

        var pending = await ListSingle();
        Assert.Equal(ApprovalStatus.PendingApproval, pending.ApprovalStatus);
        Assert.Equal(ReserveComponentStatus.Open, pending.Status);
        Assert.Equal(0m, pending.CurrentAmount);
        Assert.Single(pending.History);

        (await supervisor.TransitionAsync(reserve.ClaimId, ClaimStatus.Open)).EnsureSuccessStatusCode();
        (await supervisor.TransitionAsync(reserve.ClaimId, ClaimStatus.Closed)).EnsureSuccessStatusCode();

        Assert.Equal(ReserveComponentStatus.Closed, (await ListSingle()).Status);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await supervisor.ApproveAsync(reserve, historyId)).StatusCode);

        var rejected = await supervisor.PostAsJsonAsync($"/api/claims/{reserve.ClaimId}/reserves/{reserve.Id}/reject", new { ReserveHistoryId = historyId, Reason = "Claim closed" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(ApprovalStatus.Rejected, (await ListSingle()).ApprovalStatus);
    }
}
