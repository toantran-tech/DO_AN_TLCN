using System.Linq;
using AutoMapper;
using WashGo.Application.Contracts.Features.LockerContracts.Results;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.LockerApplication.Mappings
{
    public class LockerMappingProfile : Profile
    {
        public LockerMappingProfile()
        {
            CreateMap<Locker, LockerResultDto>()
                .ForMember(dest => dest.EmptyBoxesCount,
                    opt => opt.MapFrom(src => src.Boxes.Count(b => b.Status == LockerBoxStatus.Available)))
                .ForMember(dest => dest.MerchantName, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceAreaName, opt => opt.Ignore())
                .ForMember(dest => dest.ImageUrls, opt => opt.Ignore());
            CreateMap<LockerBox, LockerBoxResultDto>();
        }
    }
}
