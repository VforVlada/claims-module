using AutoMapper;
using ClaimsModule.Application.Reserves.Dtos;
using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.Reserves.Mappings;

public sealed class ReserveMappingProfile : Profile
{
    public ReserveMappingProfile()
    {
        CreateMap<ClaimReserveComponent, ReserveComponentDto>()
            .ForMember(d => d.CurrentAmount, o => o.MapFrom(s => s.CurrentAmount.Amount))
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.CurrentAmount.Currency));

        CreateMap<ReserveHistory, ReserveHistoryDto>()
            .ForMember(d => d.Amount, o => o.MapFrom(s => s.Amount.Amount))
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.Amount.Currency))
            .ForMember(d => d.PreviousAmount, o => o.MapFrom(s => s.PreviousAmount.Amount))
            .ForMember(d => d.NewAmount, o => o.MapFrom(s => s.NewAmount.Amount));
    }
}
