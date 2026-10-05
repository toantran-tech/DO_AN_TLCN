using AutoMapper;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.WashOrder;

namespace WashGo.Application.Features.WashOrderApplication.Mappings
{
    public class WashOrderMappingProfile : Profile
    {
        public WashOrderMappingProfile()
        {
            CreateMap<WashOrder, WashOrderResultDto>();
            CreateMap<WashOrder, WashOrderDetailDto>();
            CreateMap<WashOrderItem, WashOrderItemDto>();
            CreateMap<WashOrderTimeline, WashOrderTimelineDto>();
        }
    }
}
