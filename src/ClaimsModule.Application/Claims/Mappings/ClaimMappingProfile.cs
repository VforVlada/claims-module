using AutoMapper;
using ClaimsModule.Application.Claims.Dtos;
using ClaimsModule.Domain.Entities;

namespace ClaimsModule.Application.Claims.Mappings;

public sealed class ClaimMappingProfile : Profile
{
    public ClaimMappingProfile()
    {
        CreateMap<Claim, ClaimListItemDto>()
            .ForMember(d => d.ClaimNumber, o => o.MapFrom(s => s.ClaimNumber.Value))
            .ForMember(d => d.PolicyNumber, o => o.Ignore())
            .ForMember(d => d.ClientName, o => o.Ignore())
            .ForMember(d => d.PolicyNumber, o => o.MapFrom(s => s.Policy != null ? s.Policy.PolicyNumber : null))
            .ForMember(d => d.ClientName, o => o.MapFrom(s => s.Policy != null ? s.Policy.ClientName : null))
            .ForMember(d => d.LossDate, o => o.MapFrom(s => s.LossEvent.LossDate))
            .ForMember(d => d.CauseOfLossCode, o => o.MapFrom(s => s.LossEvent.CauseOfLossCode != null ? s.LossEvent.CauseOfLossCode.Code : string.Empty))
            .ForMember(d => d.TotalReserve, o => o.MapFrom(s => s.ReserveComponents.Sum(rc => rc.CurrentAmount.Amount)))
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.ReserveComponents.Select(rc => rc.CurrentAmount.Currency).FirstOrDefault() ?? "USD"));

        CreateMap<Claim, ClaimDetailDto>()
            .ForMember(d => d.ClaimNumber, o => o.MapFrom(s => s.ClaimNumber.Value))
            .ForMember(d => d.PolicyNumber, o => o.MapFrom(s => s.Policy != null ? s.Policy.PolicyNumber : null))
            .ForMember(d => d.ClientName, o => o.MapFrom(s => s.Policy != null ? s.Policy.ClientName : null));

        CreateMap<LossEvent, LossEventDto>()
            .ForMember(d => d.CauseOfLossCode, o => o.MapFrom(s => s.CauseOfLossCode != null ? s.CauseOfLossCode.Code : null))
            .ForMember(d => d.CauseOfLossDescription, o => o.MapFrom(s => s.CauseOfLossCode != null ? s.CauseOfLossCode.Description : null));

        CreateMap<ClaimParty, ClaimPartyDto>();
        CreateMap<ClaimRiskObject, ClaimRiskObjectDto>();
        // DownloadUrl is a short-lived SAS URL minted per request by GetClaimDocumentsQueryHandler.
        CreateMap<ClaimDocument, ClaimDocumentDto>()
            .ForMember(d => d.DownloadUrl, o => o.Ignore());
        CreateMap<ClaimAuditLog, ClaimAuditLogDto>();
    }
}
