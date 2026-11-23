using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ShipperApplication.Services;

/// <summary>Open delivery tasks of one Shipper against the effective limit.</summary>
/// <param name="ActiveTaskCount">All open tasks (lấy, trả, chuyển kho, giao lại từ kho, trả đồ đơn hủy), one per order.</param>
/// <param name="OtherTaskCount">Open tasks on orders other than the one being assigned (that one never takes a second slot).</param>
public sealed record ShipperCapacity(int ActiveTaskCount, int OtherTaskCount, int MaxActiveTasks)
{
    public int RemainingTaskCapacity => Math.Max(0, MaxActiveTasks - ActiveTaskCount);
    public bool HasRoom => OtherTaskCount < MaxActiveTasks;
}

/// <summary>
/// One rule for every assignment entry point (đơn thường, chuyển kho, giao lại từ kho, Admin/Merchant): a Shipper may hold
/// up to its own configured limit, done one after another. Counting uses
/// <see cref="DeliveryTaskRules.ActiveDeliveries"/> (the order's current task only, never history). Call the check after
/// locking the Shipper row so two assignments cannot both take the last slot.
/// </summary>
public class ShipperCapacityService(IWashOrderRepository orders, IShipperRepository shippers, IRepository<User, Guid> users,
    IRepository<Role, Guid> roles, IAsyncQueryableExecuter executer) : ITransientDependency
{
    public const string ExceededCode = "ShipperCapacityExceeded";

    public async Task<Dictionary<Guid, (int All, int Others)>> CountsAsync(IReadOnlyCollection<Guid> shipperIds, Guid? excludingOrderId)
    {
        if (shipperIds.Count == 0) return [];
        var rows = await executer.ToListAsync(DeliveryTaskRules.ActiveDeliveries(await orders.GetQueryableAsync())
            .Where(x => shipperIds.Contains(x.ShipperId!.Value))
            .Select(x => new { ShipperId = x.ShipperId!.Value, x.Id }));
        return rows.GroupBy(x => x.ShipperId)
            .ToDictionary(g => g.Key, g => (g.Count(), g.Count(x => x.Id != excludingOrderId)));
    }

    public async Task<ShipperCapacity> ForAsync(Shipper shipper, Guid? excludingOrderId)
    {
        var counts = await CountsAsync([shipper.Id], excludingOrderId);
        var (all, others) = counts.GetValueOrDefault(shipper.Id);
        return new ShipperCapacity(all, others, shipper.MaxActiveTasks);
    }

    /// <summary>meta of 409 ShipperCapacityExceeded with the latest numbers.</summary>
    public static object ExceededMeta(ShipperCapacity capacity) => new
    {
        code = ExceededCode, activeTaskCount = capacity.ActiveTaskCount, maxActiveTasks = capacity.MaxActiveTasks,
        remainingTaskCapacity = capacity.RemainingTaskCapacity
    };

    public static string ExceededMessage(ShipperCapacity capacity) =>
        $"Shipper đang có {capacity.ActiveTaskCount}/{capacity.MaxActiveTasks} nhiệm vụ chưa hoàn tất; vui lòng chọn người khác hoặc chờ Shipper hoàn tất bớt.";

    /// <summary>
    /// Shippers of the Merchant for one order's trip with capacity and the reason a row cannot be chosen. Only assignable
    /// Shippers unless <paramref name="includeUnavailable"/>.
    /// </summary>
    public async Task<List<AvailableShipperDto>> CandidatesAsync(Guid? merchantId, Guid orderId, DeliveryTaskType taskType, bool includeUnavailable)
    {
        if (!merchantId.HasValue) return [];
        var rows = await executer.ToListAsync(
            from shipper in await shippers.GetQueryableAsync()
            join user in await users.GetQueryableAsync() on shipper.UserId equals user.Id
            join role in await roles.GetQueryableAsync() on user.RoleId equals role.Id
            where shipper.MerchantId == merchantId.Value && !user.IsDeleted
            orderby user.FullName, shipper.Id
            select new
            {
                shipper.Id, shipper.MaxActiveTasks, user.FullName, user.Avatar, user.Phone, user.Email, shipper.VehicleType, shipper.VehiclePlate,
                shipper.Rating, shipper.Status, Working = shipper.IsActive,
                AccountOk = user.IsActive && role.IsActive && role.Name == "Shipper"
            });
        var counts = await CountsAsync(rows.Select(x => x.Id).ToList(), orderId);
        var result = new List<AvailableShipperDto>();
        foreach (var x in rows)
        {
            var (all, others) = counts.GetValueOrDefault(x.Id);
            var capacity = new ShipperCapacity(all, others, x.MaxActiveTasks);
            // Account first, then work status, operational status and finally the task limit.
            var blocked = !x.AccountOk ? "AccountInactive"
                : !x.Working ? "ShipperOnLeave"
                : x.Status != ShipperStatus.Available ? "ShipperNotAvailable"
                : !capacity.HasRoom ? ExceededCode
                : null;
            if (blocked != null && !includeUnavailable) continue;
            result.Add(new AvailableShipperDto
            {
                Id = x.Id, FullName = x.FullName, AvatarUrl = string.IsNullOrWhiteSpace(x.Avatar) ? null : x.Avatar, Phone = x.Phone,
                Email = x.Email, VehicleType = x.VehicleType, VehiclePlate = x.VehiclePlate, Rating = x.Rating, TaskType = taskType,
                Status = x.Status, IsWorking = x.Working,
                ActiveDeliveryCount = capacity.ActiveTaskCount, ActiveTaskCount = capacity.ActiveTaskCount,
                MaxActiveTasks = capacity.MaxActiveTasks, RemainingTaskCapacity = capacity.RemainingTaskCapacity,
                CanAssign = blocked == null, BlockedReasonCode = blocked
            });
        }
        // Choosable rows first; the order inside each group stays by name.
        return result.OrderByDescending(x => x.CanAssign).ToList();
    }
}
