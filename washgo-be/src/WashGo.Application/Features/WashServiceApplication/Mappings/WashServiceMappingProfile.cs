using AutoMapper;
using WashGo.Application.Contracts.Features.WashServiceContracts.Commands;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;
using WashGo.Domain.Entities.WashService;

namespace WashGo.Application.Features.WashServiceApplication.Mappings
{
    public class WashServiceMappingProfile : Profile
    {
        public WashServiceMappingProfile()
        {
            CreateMap<WashService, WashServiceResultDto>();
            CreateMap<CreateWashServiceCommand, WashService>();
            CreateMap<UpdateWashServiceCommand, WashService>();
        }
    }
}
