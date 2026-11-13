using Mapster;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Results;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.ServiceAreaApplication.Mappings
{
    public class ServiceAreaMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<ServiceArea, ServiceAreaResultDto>()
                .Ignore(dest => dest.MerchantCount);
            config.NewConfig<CreateServiceAreaCommand, ServiceArea>();
            config.NewConfig<UpdateServiceAreaCommand, ServiceArea>();
        }
    }
}
