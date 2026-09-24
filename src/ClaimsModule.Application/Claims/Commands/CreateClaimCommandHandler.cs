using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Application.Common.Interfaces;
using ClaimsModule.Application.Common.Models;
using ClaimsModule.Domain.Entities;
using ClaimsModule.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ClaimsModule.Application.Claims.Commands;

public sealed class CreateClaimCommandHandler(
    IApplicationDbContext context,
    IClaimNumberGenerator claimNumberGenerator,
    IReserveAuthorityEvaluator authorityEvaluator,
    ICurrentUserService currentUser,
    IMapper mapper) : IRequestHandler<CreateClaimCommand, Result<ClaimDetailDto>>
{
    public async Task<Result<ClaimDetailDto>> Handle(CreateClaimCommand request, CancellationToken cancellationToken)
    {
        var claimNumber = await claimNumberGenerator.NextAsync(cancellationToken);

        var claim = Claim.Create(
            currentUser.OrganizationEntityId,
            claimNumber,
            request.PolicyId,
            request.ClaimType,
            request.AssignedHandler,
            request.LossDate,
            request.LossDescription,
            request.LossLocation,
            request.CauseOfLossCodeId,
            currentUser.UserName);

        foreach (var party in request.Parties)
        {
            claim.AddParty(party.PartyType, party.PartyRole, party.Name, party.ContactEmail, party.ContactPhone, currentUser.UserName);
        }

        foreach (var riskObject in request.RiskObjects)
        {
            claim.AddRiskObject(riskObject.AssetType, riskObject.Description, riskObject.Identifier);
        }

        var warnings = new List<ValidationIssue>();

        if (request.PolicyId is not null)
        {
            // Loaded into the same context so EF's relationship fixup wires up claim.Policy for mapping.
            var policy = await context.Policies.FirstOrDefaultAsync(p => p.Id == request.PolicyId, cancellationToken);
            if (policy is not null && !policy.IsInForceOn(request.LossDate))
            {
                warnings.Add(new ValidationIssue("BR-C-02", "Loss date falls outside the policy's in-force period.", nameof(request.LossDate)));
            }
        }

        if (request.InitialReserve is not null)
        {
            var amount = new Money(request.InitialReserve.Amount, request.InitialReserve.Currency);
            var tier = authorityEvaluator.EvaluateTier(amount);
            claim.OpenReserve(request.InitialReserve.ComponentType, amount, tier, currentUser.UserName);

            var aggregateTotal = claim.ReserveComponents.Aggregate(Money.Zero(amount.Currency), (sum, rc) => sum + rc.CurrentAmount);
            if (authorityEvaluator.ExceedsAggregateCap(aggregateTotal))
            {
                claim.FlagManagerOverrideRequired();
                warnings.Add(new ValidationIssue("BR-R-07", "Aggregate reserve exceeds $10,000,000 and requires manager override.", "InitialReserve"));
            }
        }

        foreach (var warning in warnings)
        {
            claim.RaiseWarning(warning.Code, warning.Message, currentUser.UserName);
        }

        context.Claims.Add(claim);
        await context.SaveChangesAsync(cancellationToken);

        var dto = mapper.Map<ClaimDetailDto>(claim);
        return Result<ClaimDetailDto>.Success(dto, warnings);
    }
}
