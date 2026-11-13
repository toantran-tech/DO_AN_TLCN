using System.Net;
using WashGo.Domain.Entities.SystemConfig;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using System.Security.Cryptography;
using System.Text.Json;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Auditing;
using WashGo.Application.Contracts.Features.StorageContracts.Commands;
using WashGo.Application.Contracts.Features.StorageContracts.Queries;
using WashGo.Application.Contracts.Features.StorageContracts.Results;
using WashGo.Application.Contracts.Features.StorageContracts.Services;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Application.Features.LockerApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.StorageApplication.Services;

/// <summary>All writes serialize on the order. A dispatch/collection never claims the destination was reached.</summary>
public class StorageAppService(IWashOrderRepository orders, IRepository<OrderStorageCycle, Guid> cycles,
    IRepository<StorageReturnRequest, Guid> requests, IRepository<DeliveryTask, Guid> tasks,
    IShipperRepository shippers, IRepository<User, Guid> users, IRepository<Role, Guid> roles,
    ILockerRepository lockers, IRepository<LockerBox, Guid> boxes,
    IRepository<WashOrderTimeline, Guid> timeline, AdministrativeWriteGuard guard,
    SystemSettingProvider settings, StorageCycleCoordinator coordinator,
    StorageQuoteCodec quotes, LockerMerchantResolver lockerMerchants, NotificationPublisher notifications)
    : WashGoAppService, IStorageAppService
{
    private const string Module = "Storage";
    private string ActorRole => CurrentUser.IsInRole("Admin") ? "Admin" : CurrentUser.IsInRole("Merchant") ? "Merchant"
        : CurrentUser.IsInRole("Shipper") ? "Shipper" : CurrentUser.IsInRole("Customer") ? "Customer" : "None";

    private async Task<bool> CanReadAsync(WashOrder order) => ActorRole switch
    {
        "Admin" => true,
        "Merchant" => order.MerchantId == CurrentUser.Id,
        "Customer" => order.CustomerId == CurrentUser.Id,
        "Shipper" => await shippers.AnyAsync(x => x.Id == order.ShipperId && x.UserId == CurrentUser.Id && x.IsActive),
        _ => false
    };
    private static StorageRuleException Rule(string code, string message, int status = 400) => new(code, message, status);
    private async Task<decimal> DueAsync(WashOrder order) => OverdueFeeCalculator.GetTotalStorageDue(order, DateTime.UtcNow, await settings.ForOrderAsync(order));
    private Task<StorageReturnRequest?> ActiveRequestAsync(Guid cycleId) => requests.FirstOrDefaultAsync(x => x.CycleId == cycleId &&
        (x.State == StorageReturnState.Requested || x.State == StorageReturnState.Assigned || x.State == StorageReturnState.InTransit));
    private Task<DeliveryTask?> ActiveTaskAsync(Guid cycleId) => tasks.FirstOrDefaultAsync(x => x.StorageCycleId == cycleId && x.Status == DeliveryTaskStatus.Assigned);

    public async Task<PaginatedResult<StorageCycleDto>> GetAsync(Guid orderId, int page, int take)
    {
        if (page < 1 || take is < 1 or > 100 || (long)(page - 1) * take > int.MaxValue) return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest };
        var order = await orders.FindAsync(orderId);
        if (order == null || order.IsDeleted || !await CanReadAsync(order)) return new() { Succeeded = false, StatusCode = HttpStatusCode.NotFound };
        var query = (await cycles.GetQueryableAsync()).Where(x => x.OrderId == orderId);
        var total = await AsyncExecuter.CountAsync(query);
        var rows = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page-1)*take).Take(take));
        var result = new List<StorageCycleDto>();
        var context = await ReadContextAsync(rows);
        foreach (var row in rows) result.Add(await MapAsync(order, row, context));
        return Success(result, total, page, take);
    }

    public async Task<BaseResponse<List<AvailableShipperDto>>> GetShippersAsync(Guid orderId)
    {
        var order = await orders.FindAsync(orderId);
        if (order == null || order.IsDeleted || ActorRole is not ("Admin" or "Merchant") || !await CanReadAsync(order))
            return NotFound<List<AvailableShipperDto>>("Không tìm thấy đơn hàng.");
        var active = DeliveryTaskRules.ActiveDeliveries(await orders.GetQueryableAsync());
        var query = from s in await shippers.GetQueryableAsync()
                    join u in await users.GetQueryableAsync() on s.UserId equals u.Id
                    join r in await roles.GetQueryableAsync() on u.RoleId equals r.Id
                    where !s.IsDeleted && s.IsActive && u.IsActive && r.IsActive && r.Name == "Shipper" &&
                        s.MerchantId == order.MerchantId && s.Status == ShipperStatus.Available &&
                        !active.Any(x => x.ShipperId == s.Id && x.Id != order.Id)
                    orderby u.FullName, s.Id
                    select new AvailableShipperDto { Id = s.Id, FullName = u.FullName, Phone = u.Phone, Email = u.Email, VehicleType = s.VehicleType, VehiclePlate = s.VehiclePlate };
        return Success(await AsyncExecuter.ToListAsync(query));
    }

    private async Task<IQueryable<WashOrder>> FilterOrdersAsync(StorageFilterQuery query)
    {
        var q = (await orders.GetQueryableAsync()).Where(x => !x.IsDeleted);
        q = ActorRole switch
        {
            "Admin" => q, "Merchant" => q.Where(x => x.MerchantId == CurrentUser.Id),
            "Customer" => q.Where(x => x.CustomerId == CurrentUser.Id), _ => q.Where(x => false)
        };
        var cutoff = DateTime.UtcNow.AddHours(-72);
        q = q.Where(x => x.Status == WashOrderStatus.MovedToStorage || x.Status == WashOrderStatus.TransferringToStorage ||
            x.Status == WashOrderStatus.ReturningFromStorage || x.Status == WashOrderStatus.ReadyToReturn && x.PickupDeadline < cutoff);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var key = query.Keyword.Trim().ToLower();
            q = q.Where(x => x.OrderCode.ToLower().Contains(key));
        }
        if (query.FromDate > 0) q = q.Where(x => x.CreatedOn >= query.FromDate);
        if (query.ToDate > 0) q = q.Where(x => x.CreatedOn <= query.ToDate);
        if (query.State is { } state)
        {
            var matching = (await cycles.GetQueryableAsync()).Where(x => x.State == state).Select(x => x.Id);
            q = q.Where(x => x.ActiveStorageCycleId.HasValue && matching.Contains(x.ActiveStorageCycleId.Value) ||
                !x.ActiveStorageCycleId.HasValue && (state == StorageCycleState.Stored && x.Status == WashOrderStatus.MovedToStorage ||
                    state == StorageCycleState.AwaitingCollection && x.Status == WashOrderStatus.ReadyToReturn));
        }
        return q;
    }

    public async Task<PaginatedResult<StorageCycleDto>> FilterAsync(StorageFilterQuery query)
    {
        if (query.DateRangeError() is { } error || query.Page < 1 || query.Take is < 1 or > 100 || query.Keyword?.Length > 100 ||
            query.State.HasValue && !Enum.IsDefined(query.State.Value) || (long)(query.Page - 1) * query.Take > int.MaxValue)
            return new() { Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = "Bộ lọc/phân trang không hợp lệ." };
        var q = await FilterOrdersAsync(query);
        var total = await AsyncExecuter.CountAsync(q);
        var rows = await AsyncExecuter.ToListAsync(q.OrderByDescending(x => x.CreatedOn).ThenBy(x => x.Id).Skip((query.Page - 1) * query.Take).Take(query.Take));
        var result = new List<StorageCycleDto>();
        var ids = rows.Where(x => x.ActiveStorageCycleId.HasValue).Select(x => x.ActiveStorageCycleId!.Value).ToList();
        var cycleRows = await cycles.GetListAsync(x => ids.Contains(x.Id));
        var byId = cycleRows.ToDictionary(x => x.Id);
        var context = await ReadContextAsync(cycleRows);
        foreach (var order in rows)
            result.Add(await MapAsync(order, order.ActiveStorageCycleId.HasValue ? byId.GetValueOrDefault(order.ActiveStorageCycleId.Value) : null, context));
        return Success(result, total, query.Page, query.Take);
    }

    public async Task<BaseResponse<Dictionary<string,int>>> SummaryAsync()
    {
        var q = await FilterOrdersAsync(new StorageFilterQuery());
        var rows = await AsyncExecuter.ToListAsync(q.GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
        return Success(rows.ToDictionary(x => x.Status.ToString(), x => x.Count));
    }

    public async Task<BaseResponse<StorageQuoteDto>> QuoteAsync(Guid orderId, Guid lockerId)
    {
        var order = await orders.FindAsync(orderId);
        if (order == null || order.IsDeleted || ActorRole != "Customer" || order.CustomerId != CurrentUser.Id)
            return NotFound<StorageQuoteDto>("Không tìm thấy đơn hàng.");
        if (order.StorageWorkflowVersion == 0)
            return AdministrativeWriteGuard.Error<StorageQuoteDto>(HttpStatusCode.Conflict, "LegacyVerificationRequired", "Đơn cũ cần WashGo đối chiếu vị trí lưu trước khi yêu cầu giao lại.");
        if (order.Status != WashOrderStatus.MovedToStorage || !order.MovedToStorageAt.HasValue)
            return BadRequest<StorageQuoteDto>("Đồ chưa được ghi nhận ở kho.");
        try { await ValidateLockerAsync(order, lockerId); }
        catch (StorageRuleException ex) { return AdministrativeWriteGuard.Error<StorageQuoteDto>((HttpStatusCode)ex.Status, ex.Code, ex.Message); }
        var due = await DueAsync(order); var expiry = DateTime.UtcNow.AddMinutes(5);
        return Success(new StorageQuoteDto { OrderId = orderId, LockerId = lockerId, FeeTotal = due,
            Outstanding = Math.Max(0, due - order.StorageFeeTotal), ExpiresAt = expiry,
            QuoteToken = quotes.Write(new(orderId, order.CustomerId, lockerId, order.ActiveStorageCycleId, due, new DateTimeOffset(expiry).ToUnixTimeSeconds())) });
    }

    public Task<BaseResponse<StorageCycleDto>> AssignAsync(Guid orderId, AssignStorageShipperCommand command) =>
        WriteAsync(orderId, "Assign", command, async order =>
        {
            RequireStaff(command.Reason);
            var now = DateTime.UtcNow;
            OrderStorageCycle? cycle;
            StorageReturnRequest? request = null;
            if (order.Status == WashOrderStatus.ReadyToReturn && OverdueFeeCalculator.IsTransferToStorageDue(order.PickupDeadline, now))
                cycle = await coordinator.StartAsync(order, StorageCycleState.AwaitingCollection, now, "CapturedAtAssignment");
            else if (order.Status == WashOrderStatus.MovedToStorage && order.ActiveStorageCycleId is { } cycleId)
            {
                cycle = await cycles.FindAsync(cycleId); request = await ActiveRequestAsync(cycleId);
                if (request == null) throw Rule("ReturnRequestRequired", "Chưa có yêu cầu giao lại của khách.");
            }
            else throw Rule("WrongStage", "Đơn chưa đủ điều kiện phân công kho.");
            if (cycle == null) throw Rule("ShopAddressMissing", "Cần địa chỉ tiệm hợp lệ.");
            if (cycle.State is not (StorageCycleState.AwaitingCollection or StorageCycleState.Stored)) throw Rule("GoodsInTransit", "Không phân công lại sau khi đã lấy đồ.");
            var shipper = await shippers.GetForUpdateAsync(command.ShipperId);
            var user = shipper == null ? null : await users.FindAsync(shipper.UserId);
            if (shipper == null || user == null || shipper.IsDeleted || !shipper.IsActive || !user.IsActive ||
                shipper.Status != ShipperStatus.Available || shipper.MerchantId != order.MerchantId ||
                !await roles.AnyAsync(x => x.Id == user.RoleId && x.IsActive && x.Name == "Shipper"))
                throw Rule("ShipperNotEligible", "Shipper không đủ điều kiện hoặc không thuộc tiệm.");
            if (await AsyncExecuter.AnyAsync(DeliveryTaskRules.ActiveDeliveries(await orders.GetQueryableAsync()).Where(x => x.ShipperId == shipper.Id && x.Id != order.Id)))
                throw Rule("ShipperBusy", "Shipper còn nhiệm vụ hiệu lực.", 409);
            var previousPin = order.ShipperTaskPinCode;
            await EndAssignmentAsync(order, cycle, DeliveryTaskStatus.Reassigned);
            var type = request == null ? DeliveryTaskType.StorageTransfer : DeliveryTaskType.StorageReturn;
            if (request == null) cycle.TransferDeadline ??= now.AddHours(4);
            var task = new DeliveryTask(Guid.NewGuid()) { OrderId = order.Id, ShipperId = shipper.Id, MerchantId = order.MerchantId,
                StorageCycleId = cycle.Id, TaskType = type, Status = DeliveryTaskStatus.Assigned, AssignedAt = now, AssignedBy = CurrentUser.Id,
                SlaStartedAt = request?.RequestedAt ?? cycle.CreatedAt, SlaHours = request == null ? 4 : 24,
                Deadline = request?.Deadline ?? cycle.TransferDeadline };
            await tasks.InsertAsync(task, autoSave: true);
            if (request != null) { request.State = StorageReturnState.Assigned; await requests.UpdateAsync(request); }
            order.ShipperId = shipper.Id; order.ShipperName = user.FullName; order.AssignedTaskType = type;
            order.ShipperAssignedAt = now;
            do { order.ShipperTaskPinCode = RandomNumberGenerator.GetInt32(100000,1000000).ToString(); } while (order.ShipperTaskPinCode == previousPin);
            order.ShipperTaskQrString = $"WASHGO:{type}:{order.OrderCode}:{task.Id}:{order.ShipperTaskPinCode}";
            order.TaskPinAttemptCount = 0; order.TaskPinLockoutUntil = null;
            await notifications.SendAsync([shipper.UserId], NotificationType.NewTaskForShipper, "Nhiệm vụ kho mới",
                $"Bạn được phân công {type} cho đơn #{order.OrderCode}.", "order", order.Id);
            return cycle;
        });

    [DisableAuditing]
    public Task<BaseResponse<StorageCycleDto>> RequestReturnAsync(Guid orderId, RequestStorageReturnCommand command) =>
        WriteAsync(orderId, "RequestReturn", command, async order =>
        {
            if (ActorRole != "Customer") throw Rule("CustomerOnly", "Chỉ chủ đơn được yêu cầu giao lại.", 403);
            var now = DateTime.UtcNow;
            var quote = quotes.Read(command.QuoteToken ?? string.Empty, order.Id, order.CustomerId, now);
            if (quote == null || quote.CycleId != order.ActiveStorageCycleId) throw Rule("QuoteExpired", "Báo phí không hợp lệ hoặc đã hết hạn; lấy báo phí mới.", 409);
            await ValidateLockerAsync(order, quote.LockerId);
            var cycle = await coordinator.AdoptStoredAsync(order, await settings.ForOrderAsync(order), now);
            if (cycle == null || cycle.State != StorageCycleState.Stored) throw Rule("StorageDataMissing", "Chưa đủ dữ liệu xác nhận đồ đang ở tiệm.");
            if (quote.FeeTotal < await DueAsync(order)) throw Rule("QuoteChanged", "Phí đã thay đổi; cần xác nhận báo phí mới.", 409);
            var request = await ActiveRequestAsync(cycle.Id);
            if (request != null)
            {
                if (request.State == StorageReturnState.InTransit) throw Rule("GoodsInTransit", "Đồ đã xuất kho; không đổi trạm hoặc hủy yêu cầu.", 409);
                if (request.LockerId != quote.LockerId) throw Rule("CancelBeforeChangingLocker", "Hủy yêu cầu chưa xuất kho trước khi chọn trạm khác.", 409);
                request.AcceptedFeeTotal = quote.FeeTotal; request.FeeAcceptedAt = now;
                await requests.UpdateAsync(request); // Refresh consent, never reset requestedAt/deadline.
            }
            else
            {
                request = new StorageReturnRequest(Guid.NewGuid()) { OrderId = order.Id, CycleId = cycle.Id, LockerId = quote.LockerId,
                    State = StorageReturnState.Requested, RequestedAt = now, Deadline = now.AddHours(24), AcceptedFeeTotal = quote.FeeTotal, FeeAcceptedAt = now };
                await requests.InsertAsync(request, autoSave: true);
                await notifications.SendAsync([order.MerchantId], NotificationType.NewOrderForMerchant, "Yêu cầu giao lại từ kho",
                    $"Đơn #{order.OrderCode} cần được phân công giao lại locker.", "order", order.Id);
            }
            return cycle;
        });

    public Task<BaseResponse<StorageCycleDto>> ExecuteAsync(Guid orderId, string action, StorageCommand command) =>
        WriteAsync(orderId, action, command, async order =>
        {
            if (action == "verify-legacy")
            {
                if (ActorRole != "Admin") throw Rule("AdminOnly", "Chỉ Admin được đối chiếu legacy.", 403);
                RequireStaff(command.Reason);
                if (command is not VerifyLegacyStorageCommand verification || !verification.ConfirmedAtCurrentShop ||
                    string.IsNullOrWhiteSpace(verification.VerificationReference) || verification.VerificationReference.Trim().Length > 120)
                    throw Rule("VerificationRequired", "Cần xác nhận đồ tại tiệm và mã chứng cứ 1–120 ký tự.");
                if (order.StorageWorkflowVersion != 0 || order.ActiveStorageCycleId.HasValue)
                    throw Rule("AlreadyVerified", "Đơn không còn thuộc luồng legacy chưa xác minh.", 409);
                return await coordinator.AdoptStoredAsync(order, await settings.ForOrderAsync(order), DateTime.UtcNow, legacyVerified: true)
                    ?? throw Rule("LegacyDataMissing", "Thiếu mốc vào kho hoặc địa chỉ tiệm; không tự tạo lịch sử.");
            }
            var cycle = order.ActiveStorageCycleId.HasValue ? await cycles.FindAsync(order.ActiveStorageCycleId.Value) : null;
            if (cycle == null) throw Rule("CycleMissing", "Không có vòng lưu kho đang hoạt động.");
            if (action == "cancel-return")
            {
                if (ActorRole != "Customer") throw Rule("CustomerOnly", "Chỉ chủ đơn được hủy yêu cầu.", 403);
                var request = await ActiveRequestAsync(cycle.Id);
                if (request == null || request.State == StorageReturnState.InTransit || cycle.State != StorageCycleState.Stored)
                    throw Rule("GoodsInTransit", "Chỉ hủy yêu cầu trước khi xuất kho.", 409);
                await EndAssignmentAsync(order, cycle, DeliveryTaskStatus.Cancelled);
                request.State = StorageReturnState.Cancelled; await requests.UpdateAsync(request);
            }
            else
            {
                RequireStaff(command.Reason);
                if (action == "exception")
                {
                    if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500) throw Rule("ReasonRequired", "Cần lý do 1–500 ký tự.");
                    cycle.ExceptionNote = command.Reason.Trim();
                    await notifications.SendToAdminsAsync(NotificationType.SlaWarning, "Sự cố giao nhận kho",
                        $"Đơn #{order.OrderCode} cần hỗ trợ giao nhận; xem hồ sơ nội bộ.", "order", order.Id);
                }
                else if (action == "cancel-assignment")
                {
                    if (cycle.State is not (StorageCycleState.AwaitingCollection or StorageCycleState.Stored)) throw Rule("GoodsInTransit", "Không hủy phân công sau khi đã lấy đồ.", 409);
                    await EndAssignmentAsync(order, cycle, DeliveryTaskStatus.Cancelled);
                    var request = await ActiveRequestAsync(cycle.Id);
                    if (request != null) { request.State = StorageReturnState.Requested; await requests.UpdateAsync(request); }
                }
                else throw Rule("InvalidAction", "Thao tác không hợp lệ.");
            }
            return cycle;
        });

    [DisableAuditing]
    public Task<BaseResponse<StorageCycleDto>> ConfirmAsync(Guid orderId, string action, StorageHandoverCommand command) =>
        WriteAsync(orderId, action, command, async order =>
        {
            if (ActorRole != "Shipper") throw Rule("ShipperOnly", "Shipper được phân công phải tự xác nhận.", 403);
            var cycle = order.ActiveStorageCycleId.HasValue ? await cycles.FindAsync(order.ActiveStorageCycleId.Value) : null;
            var task = cycle == null ? null : await ActiveTaskAsync(cycle.Id);
            if (cycle == null || task == null || task.Id != command.TaskId || task.ShipperId != order.ShipperId || task.TaskType != order.AssignedTaskType)
                throw Rule("AssignmentChanged", "Phân công đã thay đổi; tải lại nhiệm vụ.", 409);
            ValidateStep(order, cycle, task, action);
            if (action == "collect" && order.PickupSessionExpiresAt > DateTime.UtcNow)
                throw Rule("PickupInProgress", "Khách đang có phiên mở tủ; chờ hoàn tất hoặc hết hạn.", 409);
            if (action == "handover" && (string.IsNullOrWhiteSpace(command.ReceiptReference) || command.ReceiptReference.Trim().Length > 120))
                throw Rule("ReceiptRequired", "Cần mã biên nhận 1–120 ký tự.");
            if (!VerifyCredential(order, command)) throw Rule("TaskCredentialInvalid", "Mã nhiệm vụ sai hoặc đang khóa.");
            var now = DateTime.UtcNow;
            if (action == "collect")
            {
                if (!cycle.SourceLockerId.HasValue || !cycle.SourceBoxId.HasValue || !await lockers.TryReleaseOccupiedBoxAsync(cycle.SourceLockerId.Value, cycle.SourceBoxId.Value))
                    throw Rule("SlotChanged", "Ô tủ không còn chứa đồ của chuyến này.", 409);
                cycle.LockerFee = OverdueFeeCalculator.Calculate(order.PickupDeadline, now, await settings.ForOrderAsync(order));
                order.AccumulatedStorageFee += cycle.LockerFee; cycle.CollectedAt = now; task.GoodsCollectedAt = now;
                cycle.State = StorageCycleState.InboundTransit; order.Status = WashOrderStatus.TransferringToStorage;
                order.PickupSessionTokenHash = null; order.PickupSessionExpiresAt = null;
            }
            else if (action == "handover")
            {
                cycle.ExceptionNote = null;
                cycle.State = StorageCycleState.Stored; cycle.IntakeAt = now; cycle.IntakeReceipt = command.ReceiptReference!.Trim();
                cycle.IntakeActorId = CurrentUser.Id; order.Status = WashOrderStatus.MovedToStorage; order.MovedToStorageAt = now;
                order.StorageLocation = cycle.ShopAddress; order.StorageReference = cycle.IntakeReceipt;
                await CompleteTaskAsync(order, cycle, task, now);
                await notifications.MovedToStorageAsync(order);
            }
            else if (action == "depart")
            {
                var request = await ActiveRequestAsync(cycle.Id) ?? throw Rule("ReturnRequestRequired", "Không có yêu cầu giao lại.");
                await ValidateLockerAsync(order, request.LockerId);
                var total = await DueAsync(order);
                if (total > request.AcceptedFeeTotal) throw Rule("QuoteChanged", "Phí tăng từ lúc báo giá; khách cần lấy/xác nhận báo phí mới.", 409);
                if (order.PaymentStatus != PaymentStatus.Paid || order.StorageFeeTotal < total) throw Rule("PaymentRequired", "Cần thanh toán đủ tiền giặt và phí kho trước khi xuất kho.", 409);
                cycle.WarehouseFee = OverdueFeeCalculator.CalculateStorageFee(cycle.IntakeAt, now, await settings.ForOrderAsync(order));
                order.AccumulatedStorageFee += cycle.WarehouseFee; cycle.DispatchedAt = now; cycle.State = StorageCycleState.OutboundTransit;
                request.State = StorageReturnState.InTransit; await requests.UpdateAsync(request);
                task.GoodsCollectedAt = now; order.Status = WashOrderStatus.ReturningFromStorage;
            }
            else
            {
                var request = await ActiveRequestAsync(cycle.Id) ?? throw Rule("ReturnRequestRequired", "Không có yêu cầu giao lại.");
                if (order.PaymentStatus != PaymentStatus.Paid || order.StorageFeeTotal < await DueAsync(order)) throw Rule("PaymentRequired", "Chưa thanh toán đủ.", 409);
                // Lock before reading station status to avoid a stale tracked Active flag after concurrent maintenance.
                await lockers.LockAsync(request.LockerId);
                await ValidateLockerAsync(order, request.LockerId);
                var minimum = order.DepositBoxSize ?? (await boxes.FindAsync(order.BoxId))?.Size ?? LockerBoxSize.Large;
                var available = await AsyncExecuter.ToListAsync((await boxes.GetQueryableAsync()).Where(x => x.LockerId == request.LockerId && !x.IsDeleted &&
                    x.Status == LockerBoxStatus.Available && x.Size >= minimum).OrderBy(x => x.Size).ThenBy(x => x.SlotNumber));
                LockerBox? chosen = null;
                foreach (var box in available)
                    if (await lockers.TryReserveBoxAsync(request.LockerId, box.Id)) { chosen = box; break; }
                if (chosen == null || !await lockers.TryOccupyReservedBoxAsync(request.LockerId, chosen.Id)) throw Rule("NoSuitableSlot", "Tủ trả hết ô phù hợp; Shipper tiếp tục giữ đồ và thử lại hoặc báo hỗ trợ.", 409);
                order.ReturnLockerId = request.LockerId; order.ReturnBoxId = chosen.Id; order.ReturnBoxNumber = chosen.BoxNumber;
                order.Status = WashOrderStatus.ReadyToReturn; order.PickupDeadline = now.AddHours(24);
                var oldPickupPin = order.PickupPinCode;
                do { order.PickupPinCode = RandomNumberGenerator.GetInt32(100000,1000000).ToString(); } while (order.PickupPinCode == oldPickupPin);
                order.QrCodeString = $"WASHGO:PICKUP:{order.OrderCode}:{order.PickupPinCode}";
                order.CustomerPickupPinAttemptCount = 0; order.CustomerPickupLockoutUntil = null;
                order.PickupSessionTokenHash = null; order.PickupSessionExpiresAt = null; order.MovedToStorageAt = null;
                cycle.ExceptionNote = null;
                cycle.State = StorageCycleState.Closed; cycle.ClosedAt = now; order.ActiveStorageCycleId = null;
                request.State = StorageReturnState.Delivered; request.DeliveredAt = now; await requests.UpdateAsync(request);
                await CompleteTaskAsync(order, cycle, task, now);
                await notifications.ReturnedToLockerAsync(order);
            }
            await tasks.UpdateAsync(task);
            return cycle;
        }, command);

    private async Task<BaseResponse<StorageCycleDto>> WriteAsync(Guid orderId, string action, StorageCommand command,
        Func<WashOrder, Task<OrderStorageCycle>> change, StorageHandoverCommand? credential = null)
    {
        if (command.Reason?.Length > 500)
            return AdministrativeWriteGuard.Error<StorageCycleDto>(HttpStatusCode.BadRequest, "ReasonTooLong", "Lý do tối đa 500 ký tự.");
        if (command.OperationId is null || command.OperationId == Guid.Empty || command.ExpectedRevision is null)
            return AdministrativeWriteGuard.Error<StorageCycleDto>(HttpStatusCode.BadRequest, "WriteGuardRequired", "Cần operationId và expectedRevision.");
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await guard.LockAsync($"admin-operation:{command.OperationId}");
        var order = await orders.GetForUpdateAsync(orderId);
        if (order == null || order.IsDeleted || !await CanReadAsync(order)) return NotFound<StorageCycleDto>("Không tìm thấy đơn hàng.");
        var replay = await guard.ReplayAsync<StorageCycleDto>(command.OperationId, CurrentUser.Id!.Value, Module, action, orderId.ToString(), command);
        if (replay != null) return replay;
        if (command.ExpectedRevision != order.Revision || command.ExpectedStatus.HasValue && command.ExpectedStatus != order.Status)
            return AdministrativeWriteGuard.Error<StorageCycleDto>(HttpStatusCode.Conflict, "RevisionMismatch", "Đơn đã thay đổi; tải lại trước khi thao tác.");
        try
        {
            var from = order.Status;
            var pendingTask = order.ActiveStorageCycleId.HasValue ? await ActiveTaskAsync(order.ActiveStorageCycleId.Value) : null;
            var cycle = await change(order);
            order.OverdueFeeTotal = await DueAsync(order);
            await orders.UpdateAsync(order, autoSave: true); await cycles.UpdateAsync(cycle, autoSave: true);
            var late = pendingTask?.Deadline < DateTime.UtcNow;
            await timeline.InsertAsync(new WashOrderTimeline(pendingTask?.Status == DeliveryTaskStatus.Completed ? pendingTask.CompletionEventId!.Value : Guid.NewGuid()) { WashOrderId = order.Id, FromStatus = from, Status = order.Status,
                ActorId = CurrentUser.Id, ActorName = CurrentUser.UserName ?? ActorRole, ActorRole = ActorRole,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), Note = $"Storage {action}" + (late ? " (Trễ SLA; vẫn cho phép giao nhận)." : ".") });
            await Audit.LogAsync(new AuditEntry(Module, action, "Order", order.Id.ToString(), order.OrderCode)
                { Reason = command.Reason, Details = new { cycleId = cycle.Id, state = cycle.State.ToString(), operationId = command.OperationId, verificationReference = (command as VerifyLegacyStorageCommand)?.VerificationReference } });
            var response = Success(await MapAsync(order, cycle), "Đã ghi nhận thao tác kho.");
            await guard.RecordAsync(command.OperationId, CurrentUser.Id!.Value, Module, action, order.Id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }
        catch (StorageRuleException ex)
        {
            if (ex.Code == "TaskCredentialInvalid")
            {
                // No business mutation precedes credential verification. Commit this attempt-only UoW, not a nested row lock.
                await orders.UpdateAsync(order, autoSave: true); await uow.CompleteAsync();
            }
            else await uow.RollbackAsync();
            var response = AdministrativeWriteGuard.Error<StorageCycleDto>((HttpStatusCode)ex.Status, ex.Code, ex.Message);
            if (ex.Code == "TaskCredentialInvalid") response.Meta = new { code = ex.Code, orderRevision = order.Revision,
                remainingAttempts = Math.Max(0, 5 - order.TaskPinAttemptCount), lockoutUntil = order.TaskPinLockoutUntil };
            return response;
        }
    }

    private void RequireStaff(string? reason)
    {
        if (ActorRole is not ("Merchant" or "Admin")) throw Rule("StaffOnly", "Chỉ Merchant hoặc Admin được điều phối.", 403);
        if (reason?.Length > 500 || ActorRole == "Admin" && string.IsNullOrWhiteSpace(reason)) throw Rule("ReasonRequired", "Admin can thiệp cần lý do 1–500 ký tự.");
    }
    private async Task ValidateLockerAsync(WashOrder order, Guid id)
    {
        var locker = await lockers.FindAsync(id);
        if (locker == null || locker.IsDeleted || locker.Status != LockerStatus.Active || !await lockerMerchants.ServesAsync(id, order.MerchantId))
            throw Rule("LockerNotEligible", "Trạm không hoạt động hoặc không thuộc phạm vi phục vụ của tiệm.");
    }
    private static void ValidateStep(WashOrder order, OrderStorageCycle cycle, DeliveryTask task, string action)
    {
        var valid = action switch
        {
            "collect" => task.TaskType == DeliveryTaskType.StorageTransfer && cycle.State == StorageCycleState.AwaitingCollection && order.Status == WashOrderStatus.ReadyToReturn,
            "handover" => task.TaskType == DeliveryTaskType.StorageTransfer && cycle.State == StorageCycleState.InboundTransit && order.Status == WashOrderStatus.TransferringToStorage,
            "depart" => task.TaskType == DeliveryTaskType.StorageReturn && cycle.State == StorageCycleState.Stored && order.Status == WashOrderStatus.MovedToStorage,
            "place" => task.TaskType == DeliveryTaskType.StorageReturn && cycle.State == StorageCycleState.OutboundTransit && order.Status == WashOrderStatus.ReturningFromStorage,
            _ => false
        };
        if (!valid) throw Rule("WrongStage", "Nhiệm vụ không ở bước xác nhận này.", 409);
    }
    private static bool VerifyCredential(WashOrder order, StorageHandoverCommand command)
    {
        var now = DateTime.UtcNow;
        if (order.TaskPinLockoutUntil > now) return false;
        if (order.TaskPinLockoutUntil.HasValue) { order.TaskPinLockoutUntil = null; order.TaskPinAttemptCount = 0; }
        var pin = !string.IsNullOrWhiteSpace(command.PinCode); var qr = !string.IsNullOrWhiteSpace(command.QrCode);
        if (pin != qr && (pin && command.PinCode == order.ShipperTaskPinCode || qr && command.QrCode == order.ShipperTaskQrString))
        { order.TaskPinAttemptCount = 0; order.TaskPinLockoutUntil = null; return true; }
        if (++order.TaskPinAttemptCount >= 5) order.TaskPinLockoutUntil = now.AddMinutes(5);
        return false;
    }
    private async Task EndAssignmentAsync(WashOrder order, OrderStorageCycle cycle, DeliveryTaskStatus state)
    {
        var previous = await ActiveTaskAsync(cycle.Id);
        if (previous != null) { previous.Status = state; previous.EndedAt = DateTime.UtcNow; await tasks.UpdateAsync(previous); }
        order.AssignedTaskType = null; order.ShipperTaskPinCode = null; order.ShipperTaskQrString = null;
        order.TaskPinAttemptCount = 0; order.TaskPinLockoutUntil = null;
    }
    private async Task CompleteTaskAsync(WashOrder order, OrderStorageCycle cycle, DeliveryTask task, DateTime now)
    {
        var lockerId = task.TaskType == DeliveryTaskType.StorageTransfer ? cycle.SourceLockerId : order.ReturnLockerId;
        var locker = lockerId.HasValue ? await lockers.FindAsync(lockerId.Value) : null;
        task.Status = DeliveryTaskStatus.Completed; task.CompletedAt = now; task.CompletedBy = CurrentUser.Id;
        task.CompletedByRole = "Shipper"; task.CompletionEventId = Guid.NewGuid(); task.SnapshotSource = "CapturedAtCompletion";
        task.OrderCodeSnapshot = order.OrderCode; task.MerchantOrderCodeSnapshot = order.MerchantOrderCode;
        task.MerchantNameSnapshot = order.MerchantName; task.OrderStatusSnapshot = order.Status;
        task.LockerIdSnapshot = lockerId; task.LockerNameSnapshot = locker?.Name; task.LockerAddressSnapshot = locker?.Address;
        task.LockerLatitudeSnapshot = locker?.Latitude; task.LockerLongitudeSnapshot = locker?.Longitude;
        task.BoxIdSnapshot = task.TaskType == DeliveryTaskType.StorageTransfer ? cycle.SourceBoxId : order.ReturnBoxId;
        task.BoxNumberSnapshot = task.TaskType == DeliveryTaskType.StorageTransfer ? order.ReturnBoxNumber ?? order.BoxNumber : order.ReturnBoxNumber;
        var shipper = await shippers.GetForUpdateAsync(task.ShipperId);
        if (shipper != null) { shipper.TotalDeliveries++; await shippers.UpdateAsync(shipper); }
        order.AssignedTaskType = null; order.ShipperTaskPinCode = null; order.ShipperTaskQrString = null;
    }
    private sealed record ReadContext(Dictionary<Guid,DeliveryTask> Tasks, Dictionary<Guid,StorageReturnRequest> Requests,
        HashSet<Guid> MyShippers, SystemSetting Settings, DateTime Now);
    private async Task<ReadContext> ReadContextAsync(List<OrderStorageCycle> rows)
    {
        var ids = rows.Select(x => x.Id).ToList();
        var deliveries = (await tasks.GetListAsync(x => x.StorageCycleId.HasValue && ids.Contains(x.StorageCycleId.Value)))
            .GroupBy(x => x.StorageCycleId!.Value).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.AssignedAt).ThenByDescending(x => x.Id).First());
        var returns = (await requests.GetListAsync(x => ids.Contains(x.CycleId)))
            .GroupBy(x => x.CycleId).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.RequestedAt).ThenByDescending(x => x.Id).First());
        var mine = ActorRole == "Shipper" ? (await shippers.GetListAsync(x => x.UserId == CurrentUser.Id && x.IsActive)).Select(x => x.Id).ToHashSet() : [];
        return new(deliveries, returns, mine, await settings.GetAsync(), DateTime.UtcNow);
    }
    private async Task<StorageCycleDto> MapAsync(WashOrder order, OrderStorageCycle? cycle, ReadContext? context = null)
    {
        var customer = ActorRole == "Customer";
        context ??= await ReadContextAsync(cycle == null ? [] : [cycle]);
        var task = cycle == null ? null : context.Tasks.GetValueOrDefault(cycle.Id);
        var request = cycle == null ? null : context.Requests.GetValueOrDefault(cycle.Id);
        var hasRequest = request?.State is StorageReturnState.Requested or StorageReturnState.Assigned or StorageReturnState.InTransit;
        var state = cycle?.State ?? (order.Status == WashOrderStatus.ReadyToReturn ? StorageCycleState.AwaitingCollection : StorageCycleState.Stored);
        var isCurrent = cycle == null || order.ActiveStorageCycleId == cycle.Id;
        var ownTask = isCurrent && task?.Status == DeliveryTaskStatus.Assigned && context.MyShippers.Contains(task.ShipperId);
        var due = OverdueFeeCalculator.GetTotalStorageDue(order, context.Now, context.Settings);
        var departureBlock = !ownTask || state != StorageCycleState.Stored ? "WrongStage" : !hasRequest ? "ReturnRequestRequired"
            : due > request!.AcceptedFeeTotal ? "QuoteChanged"
            : order.PaymentStatus != PaymentStatus.Paid || order.StorageFeeTotal < due ? "PaymentRequired" : null;
        return new StorageCycleDto
        {
            Id = cycle?.Id ?? Guid.Empty, OrderId = order.Id, OrderCode = order.OrderCode, OrderRevision = order.Revision, State = state,
            Basis = cycle?.Basis ?? "LegacyNotAdopted", ShopAddress = customer ? null : cycle?.ShopAddress,
            CollectedAt = cycle?.CollectedAt, IntakeAt = cycle?.IntakeAt, DispatchedAt = cycle?.DispatchedAt,
            ReceiptReference = customer ? null : cycle?.IntakeReceipt, ExceptionNote = customer ? null : cycle?.ExceptionNote,
            TransferDeadline = cycle?.TransferDeadline, ReturnRequestId = request?.Id, ReturnState = request?.State,
            ReturnLockerId = request?.LockerId, ReturnDeadline = request?.Deadline, FeeTotal = due, FeePaid = order.StorageFeeTotal,
            FrozenLockerFee = cycle?.LockerFee ?? 0, FrozenWarehouseFee = cycle?.WarehouseFee ?? 0,
            TaskId = customer ? null : task?.Id,
            CanAssign = isCurrent && (ActorRole is "Admin" or "Merchant") && (state == StorageCycleState.AwaitingCollection || state == StorageCycleState.Stored && hasRequest),
            CanCancelAssignment = isCurrent && (ActorRole is "Admin" or "Merchant") && task?.Status == DeliveryTaskStatus.Assigned && (state is StorageCycleState.AwaitingCollection or StorageCycleState.Stored),
            CanRequestReturn = customer && order.StorageWorkflowVersion >= 1 && isCurrent && state == StorageCycleState.Stored,
            CanCancelReturn = customer && isCurrent && state == StorageCycleState.Stored && hasRequest,
            CanCollect = ownTask && state == StorageCycleState.AwaitingCollection, CanHandover = ownTask && state == StorageCycleState.InboundTransit,
            CanDepart = departureBlock == null, DepartBlockedReason = departureBlock, CanPlace = ownTask && state == StorageCycleState.OutboundTransit,
            IsLate = (state is StorageCycleState.AwaitingCollection or StorageCycleState.InboundTransit ? cycle?.TransferDeadline : request?.Deadline) < (task?.CompletedAt ?? context.Now)
        };
    }
    public async Task<BaseResponse<StorageCycleDto>> GetOperationAsync(Guid orderId, Guid operationId)
    {
        var order = await orders.FindAsync(orderId);
        if (order == null || !await CanReadAsync(order)) return NotFound<StorageCycleDto>("Không tìm thấy đơn hàng.");
        var op = await guard.FindAsync(operationId, CurrentUser.Id ?? Guid.Empty, Module);
        return op == null || op.ResourceId != orderId.ToString() ? NotFound<StorageCycleDto>("Chưa có kết quả thao tác.")
            : JsonSerializer.Deserialize<BaseResponse<StorageCycleDto>>(op.ResponseJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
