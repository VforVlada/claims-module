using AutoMapper;
using ClaimsModule.Application.ReferenceData.Dtos;
using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.ReferenceData.Mappings;

public sealed class ReferenceDataMappingProfile : Profile
{
    public ReferenceDataMappingProfile()
    {
        CreateMap<CauseOfLossCode, CauseOfLossCodeDto>();
    }
}
