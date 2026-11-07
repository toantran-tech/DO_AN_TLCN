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
            CreateMap<WashService, WashServiceResultDto>()
                .ForMember(dest => dest.ImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.Version, opt => opt.Ignore())
                .ForMember(dest => dest.HasPendingProposal, opt => opt.Ignore())
                .ForMember(dest => dest.MyPendingProposalId, opt => opt.Ignore())
                .ForMember(dest => dest.CanProposeUpdate, opt => opt.Ignore())
                .ForMember(dest => dest.ProposeBlockedCode, opt => opt.Ignore())
                .ForMember(dest => dest.ProposeBlockedReason, opt => opt.Ignore());
            CreateMap<CreateWashServiceCommand, WashService>();
            CreateMap<UpdateWashServiceCommand, WashService>();
        }
    }
}
