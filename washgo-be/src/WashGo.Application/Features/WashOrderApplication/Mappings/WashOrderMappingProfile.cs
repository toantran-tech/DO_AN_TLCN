using AutoMapper;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.WashOrder;

namespace WashGo.Application.Features.WashOrderApplication.Mappings
{
    public class WashOrderMappingProfile : Profile
    {
        public WashOrderMappingProfile()
        {
            CreateMap<WashOrder, WashOrderResultDto>()
                .ForMember(dest => dest.BoxSize, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceNames, opt => opt.Ignore())
                .ForMember(dest => dest.IsNew, opt => opt.Ignore());
            CreateMap<WashOrder, WashOrderDetailDto>()
                .ForMember(dest => dest.BoxSize, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceNames, opt => opt.Ignore())
                .ForMember(dest => dest.IsNew, opt => opt.Ignore());
            CreateMap<WashOrderItem, WashOrderItemDto>();
            CreateMap<WashOrderTimeline, WashOrderTimelineDto>();
        }
    }
}
