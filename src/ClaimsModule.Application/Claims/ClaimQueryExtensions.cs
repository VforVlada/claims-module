using ClaimsModule.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims;

internal static class ClaimQueryExtensions
{
    public static IQueryable<Claim> IncludeFullGraph(this IQueryable<Claim> query) => query
        .Include(c => c.Policy)
        .Include(c => c.LossEvent).ThenInclude(le => le.CauseOfLossCode)
        .Include(c => c.Parties)
        .Include(c => c.RiskObjects)
        .Include(c => c.ReserveComponents).ThenInclude(rc => rc.History);
}
