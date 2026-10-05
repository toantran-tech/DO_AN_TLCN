using AutoMapper;
using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.MerchantApplication.Mappings
{
    public class MerchantMappingProfile : Profile
    {
        public MerchantMappingProfile()
        {
            CreateMap<Merchant, MerchantResultDto>();
            CreateMap<CreateMerchantCommand, Merchant>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.UserId));
            CreateMap<UpdateMerchantCommand, Merchant>();
        }
    }
}
