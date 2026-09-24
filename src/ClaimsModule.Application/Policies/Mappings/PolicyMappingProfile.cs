using AutoMapper;
using ClaimsModule.Application.Policies.Dtos;
using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.Policies.Mappings;

public sealed class PolicyMappingProfile : Profile
{
    public PolicyMappingProfile()
    {
        CreateMap<Policy, PolicySearchResultDto>()
            .ForMember(d => d.IsInForce, o => o.MapFrom(s => DateTimeOffset.UtcNow >= s.EffectiveDate && DateTimeOffset.UtcNow <= s.ExpirationDate));

        CreateMap<PolicyCoverage, PolicyCoverageDto>()
            .ForMember(d => d.Limit, o => o.MapFrom(s => s.Limit.Amount))
            .ForMember(d => d.Deductible, o => o.MapFrom(s => s.Deductible.Amount))
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.Limit.Currency));
    }
}
