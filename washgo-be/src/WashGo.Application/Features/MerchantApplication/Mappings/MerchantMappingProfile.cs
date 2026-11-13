using Mapster;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.MerchantApplication.Mappings
{
    public class MerchantMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<Merchant, MerchantResultDto>()
                .Ignore(dest => dest.Email)
                .Ignore(dest => dest.ImageUrls)
                .Ignore(dest => dest.UnfinishedOrders)
                .Ignore(dest => dest.ServiceAreaIds);
        }
    }
}
