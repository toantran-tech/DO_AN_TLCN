using WashGo.Domain.Shared.Enums;
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
                .Map(dest => dest.CategoryName, src => src.Category == null ? null : src.Category.Name)
                .Map(dest => dest.CategoryCode, src => src.Category == null ? null : src.Category.Code)
                .Map(dest => dest.CategoryIsActive, src => src.Category != null && src.Category.IsActive && !src.Category.IsDeleted)
                .Map(dest => dest.EffectiveIsActive, src => src.IsActive && !src.IsDeleted && src.ApprovalStatus == ServiceApprovalStatus.Approved && src.Category != null && src.Category.IsActive && !src.Category.IsDeleted)
                .Map(dest => dest.BlockedReason, src => src.Category == null || src.Category.IsDeleted ? "CategoryDeleted" : !src.Category.IsActive ? "CategoryInactive" : null)
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
