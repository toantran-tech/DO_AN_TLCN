using System.Linq;
using Mapster;
using WashGo.Application.Contracts.Features.LockerContracts.Results;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.LockerApplication.Mappings
{
    public class LockerMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Locker, LockerResultDto>()
                .Map(dest => dest.EmptyBoxesCount, src => src.Boxes == null ? 0 : src.Boxes.Count(b => b.Status == LockerBoxStatus.Available))
                .Map(dest => dest.Boxes, src => src.Boxes ?? new List<LockerBox>())
                .Ignore(dest => dest.Merchants)
                .Ignore(dest => dest.ServiceAreaName)
                .Ignore(dest => dest.ImageUrls);
            config.NewConfig<LockerBox, LockerBoxResultDto>();
        }
    }
}
