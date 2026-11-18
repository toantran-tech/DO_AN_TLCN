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
            // The code is issued by the server on create and never changes afterwards.
            config.NewConfig<CreateServiceAreaCommand, ServiceArea>().Ignore(dest => dest.Code);
            config.NewConfig<UpdateServiceAreaCommand, ServiceArea>().Ignore(dest => dest.Code).Ignore(dest => dest.Id);
        }
    }
}
