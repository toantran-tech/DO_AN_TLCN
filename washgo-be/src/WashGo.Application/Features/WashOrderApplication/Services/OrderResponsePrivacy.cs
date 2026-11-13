using WashGo.Application.Contracts.Features.WashOrderContracts.Results;

namespace WashGo.Application.Features.WashOrderApplication.Services;

/// <summary>Last response boundary, including enriched results, conflicts and replays. Never edits the entity.</summary>
public static class OrderResponsePrivacy
{
    public static T Apply<T>(T dto, string role) where T : WashOrderResultDto
    {
        // General order responses never reveal staff task credentials. Use the audited/scoped endpoints.
        dto.ShipperTaskPinCode = null;
        dto.ShipperTaskQrString = null;
        if (role != "Customer")
        {
            dto.DepositPinCode = string.Empty;
            dto.PickupPinCode = string.Empty;
            dto.QrCodeString = string.Empty;
        }
        if (role == "Admin") return dto;

        if (role == "Customer")
        {
            dto.MerchantId = null;
            dto.MerchantName = null;
            dto.MerchantOrderCode = null;
            dto.MerchantEmail = null;
            dto.MerchantPhoneNumber = null;
            dto.MerchantImageUrls = [];
            dto.ShipperId = null;
            dto.ShipperName = null;
            dto.ShipperAvatarUrl = null;
            dto.ShipperPhone = null;
            dto.ShipperEmail = null;
            dto.ShipperVehicleType = null;
            dto.ShipperVehiclePlate = null;
            dto.ShipperNote = null;
            dto.StorageLocation = null;
            dto.StorageReference = null;
            dto.TaskId = null;
            dto.AssignedTaskType = null;
            dto.ShipperAssignedAt = null;
            dto.CommissionRateApplied = null;
            dto.CommissionLockedAt = null;
            dto.CommissionOnCollected = null;
            dto.MerchantPayoutOnCollected = null;
        }
        else
        {
            dto.CustomerId = Guid.Empty;
            dto.CustomerName = "***";
            dto.CustomerPhoneNumber = "***";
            dto.CustomerAvatarUrl = null;
            dto.CancellationReason = null;
        }

        if (dto is WashOrderDetailDto detail)
        {
            if (role != "Customer")
            {
                detail.CustomerNote = null;
                foreach (var item in detail.Items) item.Note = null;
            }
            else
            {
                detail.AllowedActions = null;
                detail.NextAction = null;
            }
            foreach (var entry in detail.Timeline)
            {
                // Any free text may contain identity, regardless of the recorded actor role.
                entry.Note = null;
                if (role == "Customer" && entry.ActorRole != "Customer" ||
                    role != "Customer" && entry.ActorRole == "Customer")
                {
                    entry.ActorId = null;
                    entry.ActorName = role == "Customer" ? "WashGo" : "Khách hàng";
                }
            }
        }
        return dto;
    }
}
