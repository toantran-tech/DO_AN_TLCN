using Mapster;
using WashGo.Application.Contracts.Features.WashServiceContracts.Commands;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;
using WashGo.Domain.Entities.WashService;

namespace WashGo.Application.Features.WashServiceApplication.Mappings
{
    public class WashServiceMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<WashService, WashServiceResultDto>()
                .Ignore(dest => dest.ImageUrls)
                .Ignore(dest => dest.Version)
                .Ignore(dest => dest.HasPendingProposal)
                .Ignore(dest => dest.MyPendingProposalId)
                .Ignore(dest => dest.CanProposeUpdate)
                .Ignore(dest => dest.ProposeBlockedCode)
                .Ignore(dest => dest.ProposeBlockedReason);
            config.NewConfig<CreateWashServiceCommand, WashService>();
            config.NewConfig<UpdateWashServiceCommand, WashService>();
        }
    }
}
