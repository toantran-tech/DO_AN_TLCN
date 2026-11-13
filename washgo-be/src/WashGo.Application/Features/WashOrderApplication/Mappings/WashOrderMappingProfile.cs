using Mapster;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.WashOrder;

namespace WashGo.Application.Features.WashOrderApplication.Mappings
{
    public class WashOrderMappingProfile : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            config.NewConfig<WashOrder, WashOrderResultDto>()
                .Ignore(dest => dest.ProgressPercent)
                .Ignore(dest => dest.WorkflowSteps)
                .Ignore(dest => dest.BoxSize)
                .Ignore(dest => dest.ServiceNames)
                .Ignore(dest => dest.IsNew)
                .Ignore(dest => dest.Services)
                .Ignore(dest => dest.LockerImageUrls)
                .Ignore(dest => dest.ReturnLockerImageUrls)
                .Ignore(dest => dest.MerchantPhoneNumber)
                .Ignore(dest => dest.MerchantImageUrls)
                .Ignore(dest => dest.CustomerAvatarUrl)
                .Ignore(dest => dest.ShipperAvatarUrl)
                .Ignore(dest => dest.ShipperPhone)
                .Ignore(dest => dest.ShipperEmail)
                .Ignore(dest => dest.ShipperVehicleType)
                .Ignore(dest => dest.ShipperVehiclePlate)
                .Ignore(dest => dest.CollectedLaundryAmount)
                .Ignore(dest => dest.RefundedAmount)
                .Ignore(dest => dest.CommissionOnCollected)
                .Ignore(dest => dest.MerchantPayoutOnCollected)
                .Ignore(dest => dest.FinancialStatus)
                .Ignore(dest => dest.SettledNetAmount)
                .Ignore(dest => dest.UnsettledNetAmount);
            config.NewConfig<WashOrder, WashOrderDetailDto>()
                .Inherits<WashOrder, WashOrderResultDto>()
                .Map(dest => dest.Items, src => src.Items ?? new List<WashOrderItem>())
                .Map(dest => dest.Timeline, src => src.Timeline ?? new List<WashOrderTimeline>())
                .Ignore(dest => dest.TaskLockerName)
                .Ignore(dest => dest.TaskLockerAddress)
                .Ignore(dest => dest.TaskBoxNumber)
                .Ignore(dest => dest.NextAction)
                .Ignore(dest => dest.ReturnLockerName)
                .Ignore(dest => dest.ReturnLockerAddress)
                .Ignore(dest => dest.ReturnLockerLatitude)
                .Ignore(dest => dest.ReturnLockerLongitude)
                .Ignore(dest => dest.LockerAddress)
                .Ignore(dest => dest.LockerLatitude)
                .Ignore(dest => dest.LockerLongitude)
                .Ignore(dest => dest.PredictedFinishAt)
                .Ignore(dest => dest.PredictionStatus)
                .Ignore(dest => dest.PredictionSource)
                .Ignore(dest => dest.PredictionSampleSize)
                .Ignore(dest => dest.PredictionLoadFactor)
                .Ignore(dest => dest.PredictionUpdatedAt)
                .Ignore(dest => dest.AllowedActions);
            config.NewConfig<WashOrderItem, WashOrderItemDto>()
                .Ignore(dest => dest.ServiceImageUrls);
            config.NewConfig<WashOrderTimeline, WashOrderTimelineDto>();
        }
    }
}
