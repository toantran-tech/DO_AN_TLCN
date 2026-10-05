using AutoMapper;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Results;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.ServiceAreaApplication.Mappings
{
    public class ServiceAreaMappingProfile : Profile
    {
        public ServiceAreaMappingProfile()
        {
            CreateMap<ServiceArea, ServiceAreaResultDto>();
            CreateMap<CreateServiceAreaCommand, ServiceArea>();
            CreateMap<UpdateServiceAreaCommand, ServiceArea>();
        }
    }
}
