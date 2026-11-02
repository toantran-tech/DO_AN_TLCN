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
            CreateMap<Merchant, MerchantResultDto>()
                .ForMember(dest => dest.Email, opt => opt.Ignore())
                .ForMember(dest => dest.ImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.UnfinishedOrders, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceAreaIds, opt => opt.Ignore());
        }
    }
}
