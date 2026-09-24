using AutoMapper;
using ClaimsModule.Application.Policies.Dtos;
using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.Policies.Mappings;

public sealed class PolicyMappingProfile : Profile
{
    public PolicyMappingProfile()
    {
        // "today" is a ProjectTo parameter (supplied from IDateTimeProvider by the handler), so the
        // status is computed in SQL against the application's clock, not the database server's.
        DateTimeOffset today = default;

        CreateMap<Policy, PolicySearchResultDto>()
            .ForMember(d => d.PolicyId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.Status, o => o.MapFrom(s =>
                today < s.EffectiveDate ? PolicyStatus.NotYetEffective
                : today > s.ExpirationDate ? PolicyStatus.Expired
                : PolicyStatus.Active));

        CreateMap<PolicyCoverage, PolicyCoverageDto>()
            .ForMember(d => d.Limit, o => o.MapFrom(s => s.Limit.Amount))
            .ForMember(d => d.Deductible, o => o.MapFrom(s => s.Deductible.Amount))
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.Limit.Currency));
    }
}
