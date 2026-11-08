using AutoMapper;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.WashOrder;

namespace WashGo.Application.Features.WashOrderApplication.Mappings
{
    public class WashOrderMappingProfile : Profile
    {
        public WashOrderMappingProfile()
        {
            CreateMap<WashOrder, WashOrderResultDto>()
                .ForMember(dest => dest.ProgressPercent, opt => opt.Ignore())
                .ForMember(dest => dest.WorkflowSteps, opt => opt.Ignore())
                .ForMember(dest => dest.BoxSize, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceNames, opt => opt.Ignore())
                .ForMember(dest => dest.IsNew, opt => opt.Ignore())
                .ForMember(dest => dest.Services, opt => opt.Ignore())
                .ForMember(dest => dest.LockerImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.ReturnLockerImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.MerchantPhoneNumber, opt => opt.Ignore())
                .ForMember(dest => dest.MerchantImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerAvatarUrl, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperAvatarUrl, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperPhone, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperEmail, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperVehicleType, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperVehiclePlate, opt => opt.Ignore())
                .ForMember(dest => dest.CollectedLaundryAmount, opt => opt.Ignore())
                .ForMember(dest => dest.RefundedAmount, opt => opt.Ignore())
                .ForMember(dest => dest.CommissionOnCollected, opt => opt.Ignore())
                .ForMember(dest => dest.MerchantPayoutOnCollected, opt => opt.Ignore())
                .ForMember(dest => dest.FinancialStatus, opt => opt.Ignore());
            CreateMap<WashOrder, WashOrderDetailDto>()
                .ForMember(dest => dest.ProgressPercent, opt => opt.Ignore())
                .ForMember(dest => dest.WorkflowSteps, opt => opt.Ignore())
                .ForMember(dest => dest.BoxSize, opt => opt.Ignore())
                .ForMember(dest => dest.ServiceNames, opt => opt.Ignore())
                .ForMember(dest => dest.IsNew, opt => opt.Ignore())
                .ForMember(dest => dest.TaskLockerName, opt => opt.Ignore())
                .ForMember(dest => dest.TaskLockerAddress, opt => opt.Ignore())
                .ForMember(dest => dest.TaskBoxNumber, opt => opt.Ignore())
                .ForMember(dest => dest.NextAction, opt => opt.Ignore())
                .ForMember(dest => dest.ReturnLockerName, opt => opt.Ignore())
                .ForMember(dest => dest.ReturnLockerAddress, opt => opt.Ignore())
                .ForMember(dest => dest.ReturnLockerLatitude, opt => opt.Ignore())
                .ForMember(dest => dest.ReturnLockerLongitude, opt => opt.Ignore())
                .ForMember(dest => dest.LockerAddress, opt => opt.Ignore())
                .ForMember(dest => dest.LockerLatitude, opt => opt.Ignore())
                .ForMember(dest => dest.LockerLongitude, opt => opt.Ignore())
                .ForMember(dest => dest.PredictedFinishAt, opt => opt.Ignore())
                .ForMember(dest => dest.PredictionStatus, opt => opt.Ignore())
                .ForMember(dest => dest.PredictionSource, opt => opt.Ignore())
                .ForMember(dest => dest.PredictionSampleSize, opt => opt.Ignore())
                .ForMember(dest => dest.PredictionLoadFactor, opt => opt.Ignore())
                .ForMember(dest => dest.PredictionUpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.AllowedActions, opt => opt.Ignore())
                .ForMember(dest => dest.Services, opt => opt.Ignore())
                .ForMember(dest => dest.LockerImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.ReturnLockerImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.MerchantPhoneNumber, opt => opt.Ignore())
                .ForMember(dest => dest.MerchantImageUrls, opt => opt.Ignore())
                .ForMember(dest => dest.CustomerAvatarUrl, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperAvatarUrl, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperPhone, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperEmail, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperVehicleType, opt => opt.Ignore())
                .ForMember(dest => dest.ShipperVehiclePlate, opt => opt.Ignore())
                .ForMember(dest => dest.CollectedLaundryAmount, opt => opt.Ignore())
                .ForMember(dest => dest.RefundedAmount, opt => opt.Ignore())
                .ForMember(dest => dest.CommissionOnCollected, opt => opt.Ignore())
                .ForMember(dest => dest.MerchantPayoutOnCollected, opt => opt.Ignore())
                .ForMember(dest => dest.FinancialStatus, opt => opt.Ignore());
            CreateMap<WashOrderItem, WashOrderItemDto>()
                .ForMember(dest => dest.ServiceImageUrls, opt => opt.Ignore());
            CreateMap<WashOrderTimeline, WashOrderTimelineDto>();
        }
    }
}
