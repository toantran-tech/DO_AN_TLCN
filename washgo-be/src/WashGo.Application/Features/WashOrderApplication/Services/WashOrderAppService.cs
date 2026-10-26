using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Queries;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    public class WashOrderAppService : WashGoAppService, IWashOrderAppService
    {
        private readonly IWashOrderRepository _orderRepo;
        private readonly ILockerRepository _lockerRepo;
        private readonly IWashServiceRepository _serviceRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<Customer, Guid> _customerRepo;

        public WashOrderAppService(
            IWashOrderRepository orderRepo,
            ILockerRepository lockerRepo,
            IWashServiceRepository serviceRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<Customer, Guid> customerRepo)
        {
            _orderRepo = orderRepo;
            _lockerRepo = lockerRepo;
            _serviceRepo = serviceRepo;
            _userRepo = userRepo;
            _merchantRepo = merchantRepo;
            _customerRepo = customerRepo;
        }

        public async Task<PaginatedResult<WashOrderResultDto>> GetListAsync(WashOrderFilterQuery query)
        {
            var (data, total) = await _orderRepo.FilterProjectedDataAsync<WashOrderResultDto>(
                filterFunc: q =>
                {
                    if (query.Status.HasValue)
                        q = q.Where(x => x.Status == query.Status.Value);
                    if (query.PaymentStatus.HasValue)
                        q = q.Where(x => x.PaymentStatus == query.PaymentStatus.Value);
                    if (query.LockerId.HasValue)
                        q = q.Where(x => x.LockerId == query.LockerId.Value);
                    if (query.CustomerId.HasValue)
                        q = q.Where(x => x.CustomerId == query.CustomerId.Value);
                    if (query.ShipperId.HasValue)
                        q = q.Where(x => x.ShipperId == query.ShipperId.Value);
                    if (query.MerchantId.HasValue)
                        q = q.Where(x => x.MerchantId == query.MerchantId.Value);
                    if (!string.IsNullOrWhiteSpace(query.Keyword))
                    {
                        var kw = query.Keyword.Trim().ToLower();
                        q = q.Where(x => x.OrderCode.ToLower().Contains(kw) ||
                                         x.CustomerName.ToLower().Contains(kw) ||
                                         x.CustomerPhoneNumber.Contains(kw));
                    }
                    return q;
                },
                parameters: query,
                defaultOrder: q => q.OrderByDescending(x => x.CreatedOn)
            );

            return Success(data, total, query.Page, query.Take);
        }

        public Task<PaginatedResult<WashOrderResultDto>> GetMyOrdersAsync(WashOrderFilterQuery query)
        {
            query.CustomerId = CurrentUser.Id ?? Guid.Empty;
            return GetListAsync(query);
        }

        public async Task<BaseResponse<WashOrderDetailDto>> GetByIdAsync(Guid id)
        {
            var order = await _orderRepo.GetWithDetailsAsync(id);
            if (order == null)
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng!");
            }

            if (CurrentUser.IsInRole("Customer") &&
                (!CurrentUser.Id.HasValue || order.CustomerId != CurrentUser.Id.Value))
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng của khách hàng.");
            }

            var dto = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(dto);
        }

        public async Task<BaseResponse<WashOrderDetailDto>> CreateAsync(CreateWashOrderCommand command)
        {
            if (!CurrentUser.Id.HasValue)
            {
                return BadRequest<WashOrderDetailDto>("Không xác định được tài khoản khách hàng.");
            }

            if (command.Items == null || command.Items.Count is < 1 or > 30 ||
                command.Items.Any(x => x.ServiceId == Guid.Empty || x.Quantity is < 1 or > 1000 ||
                    (x.WeightKg.HasValue && (x.WeightKg.Value <= 0 || x.WeightKg.Value > 1000))) ||
                command.Items.Select(x => x.ServiceId).Distinct().Count() != command.Items.Count)
                return BadRequest<WashOrderDetailDto>("Cần chọn 1–30 dịch vụ khác nhau; số lượng và khối lượng phải hợp lệ.");

            if (!Enum.IsDefined(command.PaymentMethod))
                return BadRequest<WashOrderDetailDto>("Phương thức thanh toán không hợp lệ.");

            var customerId = CurrentUser.Id.Value;
            var locker = await _lockerRepo.GetWithBoxesAsync(command.LockerId);
            if (locker == null || locker.Status != LockerStatus.Active)
            {
                return BadRequest<WashOrderDetailDto>("Tủ Locker đã chọn không hoạt động.");
            }

            var merchant = await _merchantRepo.FindAsync(locker.MerchantId);
            if (merchant == null || merchant.Status != MerchantStatus.Active)
                return BadRequest<WashOrderDetailDto>("Đối tác quản lý tủ hiện không hoạt động.");

            if (command.ReturnLockerId.HasValue && command.ReturnLockerId.Value != locker.Id)
            {
                var returnLocker = await _lockerRepo.FindAsync(command.ReturnLockerId.Value);
                if (returnLocker == null || returnLocker.Status != LockerStatus.Active ||
                    returnLocker.MerchantId != locker.MerchantId)
                    return BadRequest<WashOrderDetailDto>("Tủ trả đồ phải đang hoạt động và thuộc cùng đối tác.");
            }

            var box = locker.Boxes.FirstOrDefault(b => b.Id == command.BoxId && !b.IsDeleted);
            if (box == null || box.Status != LockerBoxStatus.Available)
            {
                return BadRequest<WashOrderDetailDto>("Ô tủ đã chọn không khả dụng hoặc đã có người sử dụng!");
            }

            var customerUser = await _userRepo.FindAsync(customerId);
            if (customerUser == null || !customerUser.IsActive || await _customerRepo.FindAsync(customerId) == null)
            {
                return BadRequest<WashOrderDetailDto>("Không tìm thấy tài khoản khách hàng.");
            }

            var customerName = string.IsNullOrWhiteSpace(customerUser.FullName)
                ? "Khách hàng WashGo"
                : customerUser.FullName;
            var customerPhone = customerUser.Phone ?? string.Empty;

            var random = new Random();
            var depositPin = random.Next(100000, 999999).ToString();
            var pickupPin = random.Next(100000, 999999).ToString();
            var orderCode = $"WG{DateTime.UtcNow:yyMMdd}{random.Next(1000, 9999)}";

            var order = new WashOrder
            {
                OrderCode = orderCode,
                CustomerId = customerId,
                CustomerName = customerName,
                CustomerPhoneNumber = customerPhone,
                MerchantId = locker.MerchantId,
                LockerId = locker.Id,
                LockerName = locker.Name,
                BoxId = box.Id,
                BoxNumber = box.BoxNumber,
                ReturnLockerId = command.ReturnLockerId ?? locker.Id,
                PaymentMethod = command.PaymentMethod,
                PaymentStatus = PaymentStatus.Unpaid,
                Status = WashOrderStatus.Pending,
                DepositPinCode = depositPin,
                PickupPinCode = pickupPin,
                QrCodeString = $"WASHGO_ORDER:{orderCode}:PIN:{depositPin}",
                CustomerNote = command.CustomerNote,
                ExpectedDeliveryTime = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds()
            };

            decimal total = 0;
            foreach (var item in command.Items)
            {
                var service = await _serviceRepo.FindAsync(item.ServiceId);
                if (service == null || service.IsDeleted || !service.IsActive ||
                    service.ApprovalStatus != ServiceApprovalStatus.Approved ||
                    service.MerchantId != locker.MerchantId)
                    return BadRequest<WashOrderDetailDto>("Dịch vụ đã chọn không còn được bán bởi đối tác của tủ.");

                if (item.WeightKg.HasValue && !service.PricePerKg.HasValue)
                    return BadRequest<WashOrderDetailDto>("Dịch vụ đã chọn không hỗ trợ tính giá theo kg.");
                if (!item.WeightKg.HasValue && service.Unit.Equals("kg", StringComparison.OrdinalIgnoreCase))
                    return BadRequest<WashOrderDetailDto>("Vui lòng nhập khối lượng cho dịch vụ tính theo kg.");

                var unitPrice = item.WeightKg.HasValue
                    ? service.PricePerKg!.Value
                    : service.PricePerItem ?? service.UnitPrice;
                if (unitPrice <= 0)
                    return BadRequest<WashOrderDetailDto>("Giá dịch vụ chưa được cấu hình hợp lệ.");
                var subTotal = unitPrice * (item.WeightKg ?? item.Quantity);
                total += subTotal;
                order.Items.Add(new WashOrderItem
                {
                    ServiceId = service.Id,
                    ServiceName = service.Name,
                    Quantity = item.Quantity,
                    WeightKg = item.WeightKg,
                    UnitPrice = unitPrice,
                    SubTotal = subTotal,
                    Note = item.Note
                });
            }

            order.TotalAmount = total;
            order.FinalAmount = total;

            order.Timeline.Add(new WashOrderTimeline
            {
                Status = WashOrderStatus.Pending,
                ActorId = customerId,
                ActorName = customerName,
                ActorRole = "Customer",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = "Khách hàng khởi tạo đơn qua App/Web"
            });

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (!await _lockerRepo.TryReserveBoxAsync(locker.Id, box.Id))
                return BadRequest<WashOrderDetailDto>("Ô tủ vừa được người khác đặt; vui lòng chọn ô khác.");
            await _orderRepo.InsertAsync(order, autoSave: true);
            await uow.CompleteAsync();

            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Created(result, "Tạo đơn hàng WashGo thành công!");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> CancelAsync(Guid orderId, CancelWashOrderCommand command)
        {
            if (!CurrentUser.Id.HasValue)
            {
                return BadRequest<WashOrderDetailDto>("Không xác định được tài khoản khách hàng.");
            }

            var reason = command.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest<WashOrderDetailDto>("Vui lòng nhập lý do hủy đơn.");
            }

            if (reason.Length > 500)
            {
                return BadRequest<WashOrderDetailDto>("Lý do hủy đơn tối đa 500 ký tự.");
            }

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || order.CustomerId != CurrentUser.Id.Value)
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng của khách hàng.");
            }

            if (order.Status != WashOrderStatus.Pending)
            {
                return BadRequest<WashOrderDetailDto>("Chỉ có thể hủy đơn trước khi gửi đồ vào locker.");
            }

            if (!await _orderRepo.TryChangeStatusAsync(order.Id, WashOrderStatus.Pending, WashOrderStatus.Cancelled))
                return BadRequest<WashOrderDetailDto>("Đơn hàng vừa đổi trạng thái; không thể hủy.");
            if (!await _lockerRepo.TryReleaseReservedBoxAsync(order.LockerId, order.BoxId))
                return BadRequest<WashOrderDetailDto>("Ô tủ không còn ở trạng thái giữ chỗ; chưa hủy đơn.");

            order.Status = WashOrderStatus.Cancelled;
            order.CancelledAt = DateTime.UtcNow;
            order.CancelledBy = CurrentUser.Id.Value;
            order.CancelledByType = OrderActorType.Customer;
            order.CancellationReason = reason;
            order.CancelFeeAmount = 0;
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = WashOrderStatus.Pending,
                Status = WashOrderStatus.Cancelled,
                ActorId = CurrentUser.Id.Value,
                ActorName = CurrentUser.UserName ?? order.CustomerName,
                ActorRole = "Customer",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Khách hàng hủy đơn. Lý do: {reason}"
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            return Success(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), "Hủy đơn hàng thành công.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> UpdateStatusAsync(UpdateWashOrderStatusCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orderRepo.GetWithDetailsAsync(command.OrderId);
            if (order == null)
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng!");
            }

            var previousStatus = order.Status;
            if (!IsValidTransition(previousStatus, command.NewStatus))
            {
                return BadRequest<WashOrderDetailDto>(
                    $"Không thể chuyển trạng thái từ {previousStatus} sang {command.NewStatus}.");
            }

            // Payment enforcement rule: ReadyToReturn, Returned, Completed require PAID
            var isReturningOrComplete = command.NewStatus is WashOrderStatus.ReadyToReturn or WashOrderStatus.Returned or WashOrderStatus.Completed;
            if (isReturningOrComplete && order.PaymentStatus != PaymentStatus.Paid && command.NewStatus != WashOrderStatus.Paid)
            {
                return BadRequest<WashOrderDetailDto>(
                    "Đơn hàng phải được thanh toán (PAID) trước khi chuyển sang trạng thái trả đồ.");
            }

            if (command.NewStatus == WashOrderStatus.Returned && order.PickupDeadline.HasValue)
            {
                if (OverdueFeeCalculator.IsTransferToStorageDue(order.PickupDeadline, DateTime.UtcNow) &&
                    !order.MovedToStorageAt.HasValue)
                {
                    return BadRequest<WashOrderDetailDto>(
                        "Đơn đã quá 72 giờ quá hạn; Shipper/Admin cần ghi nhận chuyển đồ sang kho trước khi khách nhận.");
                }

                var overdueTotal = OverdueFeeCalculator.Calculate(
                    order.PickupDeadline, DateTime.UtcNow);
                order.OverdueFeeTotal = overdueTotal;
                if (order.StorageFeeTotal < overdueTotal)
                {
                    await _orderRepo.UpdateAsync(order, autoSave: true);
                    await uow.CompleteAsync();
                    return BadRequest<WashOrderDetailDto>(
                        $"Cần thanh toán phí lưu kho còn thiếu {overdueTotal - order.StorageFeeTotal:N0}đ trước khi hoàn tất nhận đồ.");
                }
            }

            if (command.NewStatus == WashOrderStatus.Paid)
            {
                order.PaymentStatus = PaymentStatus.Paid;
            }

            if (!await _orderRepo.TryChangeStatusAsync(order.Id, previousStatus, command.NewStatus))
                return BadRequest<WashOrderDetailDto>("Đơn hàng vừa đổi trạng thái; vui lòng tải lại trước khi cập nhật.");

            if (previousStatus == WashOrderStatus.Pending && command.NewStatus == WashOrderStatus.Cancelled)
            {
                if (!await _lockerRepo.TryReleaseReservedBoxAsync(order.LockerId, order.BoxId))
                    return BadRequest<WashOrderDetailDto>("Ô tủ không còn ở trạng thái giữ chỗ; chưa hủy đơn.");
            }
            else if (previousStatus == WashOrderStatus.Pending && command.NewStatus == WashOrderStatus.Deposited)
            {
                if (!await _lockerRepo.TryOccupyReservedBoxAsync(order.LockerId, order.BoxId))
                    return BadRequest<WashOrderDetailDto>("Ô tủ không còn ở trạng thái giữ chỗ; chưa nhận đồ.");
            }
            else if (previousStatus == WashOrderStatus.Deposited &&
                     command.NewStatus is WashOrderStatus.Collected or WashOrderStatus.Cancelled)
            {
                if (!await _lockerRepo.TryReleaseOccupiedBoxAsync(order.LockerId, order.BoxId))
                    return BadRequest<WashOrderDetailDto>("Ô gửi đồ không còn ở trạng thái chứa đồ; chưa đổi trạng thái đơn.");
            }
            else if (command.NewStatus == WashOrderStatus.ReadyToReturn)
            {
                var returnLockerId = order.ReturnLockerId ?? order.LockerId;
                var returnLocker = await _lockerRepo.GetWithBoxesAsync(returnLockerId);
                if (returnLocker == null || returnLocker.Status != LockerStatus.Active ||
                    returnLocker.MerchantId != order.MerchantId)
                    return BadRequest<WashOrderDetailDto>("Tủ trả đồ không còn hoạt động hoặc không thuộc đối tác của đơn.");

                LockerBox? returnBox = null;
                foreach (var candidate in returnLocker.Boxes.Where(x => !x.IsDeleted &&
                             x.Status == LockerBoxStatus.Available).OrderBy(x => x.SlotNumber))
                {
                    if (!await _lockerRepo.TryReserveBoxAsync(returnLockerId, candidate.Id))
                        continue;
                    if (!await _lockerRepo.TryOccupyReservedBoxAsync(returnLockerId, candidate.Id))
                        return BadRequest<WashOrderDetailDto>("Không thể xác nhận ô trả đồ; vui lòng thử lại.");
                    returnBox = candidate;
                    break;
                }
                if (returnBox == null)
                    return BadRequest<WashOrderDetailDto>("Tủ trả đồ đã hết ô trống; chưa chuyển trạng thái đơn.");

                order.ReturnLockerId = returnLockerId;
                order.ReturnBoxId = returnBox.Id;
                order.ReturnBoxNumber = returnBox.BoxNumber;
            }
            else if (previousStatus == WashOrderStatus.ReadyToReturn &&
                     command.NewStatus == WashOrderStatus.Returned)
            {
                var (returnLockerId, returnBoxId) = GetReturnSlot(order);
                if (!await _lockerRepo.TryReleaseOccupiedBoxAsync(returnLockerId, returnBoxId))
                    return BadRequest<WashOrderDetailDto>("Ô trả đồ không còn chứa đồ; chưa xác nhận khách nhận.");
            }

            order.Status = command.NewStatus;
            if (command.NewStatus == WashOrderStatus.ReadyToReturn)
            {
                // Customers have 24 hours to collect the laundry from the return locker before storage fees start.
                order.PickupDeadline = DateTime.UtcNow.AddHours(24);
            }
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = previousStatus,
                Status = command.NewStatus,
                ActorId = command.ActorId,
                ActorName = command.ActorName ?? "Hệ thống",
                ActorRole = command.ActorRole ?? "System",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = command.Note ?? $"Chuyển trạng thái từ {previousStatus} sang {command.NewStatus}"
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(result, "Cập nhật trạng thái đơn thành công!");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> MoveToStorageAsync(MoveOrderToStorageCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.StorageLocation) || command.StorageLocation.Trim().Length > 200)
                return BadRequest<WashOrderDetailDto>("Cần nhập vị trí kho hợp lệ (tối đa 200 ký tự).");
            if (string.IsNullOrWhiteSpace(command.StorageReference) || command.StorageReference.Trim().Length > 120)
                return BadRequest<WashOrderDetailDto>("Cần nhập mã biên nhận/vị trí lưu kho (tối đa 120 ký tự).");
            if (!string.IsNullOrWhiteSpace(command.Note) && command.Note.Trim().Length > 140)
                return BadRequest<WashOrderDetailDto>("Ghi chú chuyển kho tối đa 140 ký tự.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orderRepo.GetWithDetailsAsync(command.OrderId);
            if (order == null)
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng.");
            if (order.Status != WashOrderStatus.ReadyToReturn)
                return BadRequest<WashOrderDetailDto>("Chỉ chuyển kho cho đồ đang nằm tại locker trả.");
            if (order.MovedToStorageAt.HasValue)
                return BadRequest<WashOrderDetailDto>("Đơn hàng đã được ghi nhận chuyển kho trước đó.");

            var now = DateTime.UtcNow;
            if (!OverdueFeeCalculator.IsTransferToStorageDue(order.PickupDeadline, now))
                return BadRequest<WashOrderDetailDto>("Chỉ chuyển kho khi đã quá 72 giờ kể từ hạn nhận đồ.");

            var previousStatus = order.Status;
            if (!await _orderRepo.TryChangeStatusAsync(order.Id, WashOrderStatus.ReadyToReturn,
                    WashOrderStatus.MovedToStorage))
                return BadRequest<WashOrderDetailDto>("Đơn hàng vừa đổi trạng thái; chưa chuyển kho.");
            var (returnLockerId, returnBoxId) = GetReturnSlot(order);
            if (!await _lockerRepo.TryReleaseOccupiedBoxAsync(returnLockerId, returnBoxId))
                return BadRequest<WashOrderDetailDto>("Ô trả đồ không còn chứa đồ; chưa chuyển kho.");

            order.Status = WashOrderStatus.MovedToStorage;
            order.MovedToStorageAt = now;
            order.StorageLocation = command.StorageLocation.Trim();
            order.StorageReference = command.StorageReference.Trim();
            order.OverdueFeeTotal = OverdueFeeCalculator.Calculate(order.PickupDeadline, now);
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = previousStatus,
                Status = WashOrderStatus.MovedToStorage,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? "Shipper/Admin",
                ActorRole = "ShipperOrAdmin",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = command.Note?.Trim() is { Length: > 0 } note
                    ? $"Đã chuyển đồ sang kho {order.StorageLocation}, mã lưu kho {order.StorageReference}. {note}"
                    : $"Đã chuyển đồ sang kho {order.StorageLocation}, mã lưu kho {order.StorageReference}."
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            return Success(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), "Đã ghi nhận chuyển đồ sang kho và giải phóng ô locker.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> AssignShipperAsync(AssignShipperCommand command)
        {
            var order = await _orderRepo.GetWithDetailsAsync(command.OrderId);
            if (order == null)
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng!");
            }

            order.ShipperId = command.ShipperId;
            order.ShipperName = command.ShipperName;
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                Status = order.Status,
                ActorId = command.ShipperId,
                ActorName = command.ShipperName,
                ActorRole = "Shipper",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Phân công đơn hàng cho Shipper {command.ShipperName}"
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(result, "Phân công Shipper thành công!");
        }

        private static bool IsValidTransition(WashOrderStatus current, WashOrderStatus next)
        {
            if (next == WashOrderStatus.Cancelled)
            {
                return current is WashOrderStatus.Pending or WashOrderStatus.Deposited;
            }

            return current switch
            {
                WashOrderStatus.Pending => next == WashOrderStatus.Deposited,
                WashOrderStatus.Deposited => next == WashOrderStatus.Collected,
                WashOrderStatus.Collected => next == WashOrderStatus.Washing,
                WashOrderStatus.Washing => next is WashOrderStatus.Drying or WashOrderStatus.Ironing or WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Drying => next is WashOrderStatus.Ironing or WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Ironing => next is WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Folding => next == WashOrderStatus.AwaitingPayment,
                WashOrderStatus.AwaitingPayment => next == WashOrderStatus.Paid,
                WashOrderStatus.Paid => next == WashOrderStatus.ReadyToReturn,
                WashOrderStatus.ReadyToReturn => next == WashOrderStatus.Returned,
                WashOrderStatus.MovedToStorage => next == WashOrderStatus.Returned,
                WashOrderStatus.Returned => next == WashOrderStatus.Completed,
                _ => false
            };
        }

        private static (Guid LockerId, Guid BoxId) GetReturnSlot(WashOrder order) =>
            order.ReturnBoxId.HasValue
                ? (order.ReturnLockerId ?? order.LockerId, order.ReturnBoxId.Value)
                : (order.LockerId, order.BoxId); // Orders created before ReturnBoxId was recorded.

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, WashOrderFilterQuery query)
        {
            var result = await _orderRepo.GetColumnDistinctValuesAsync(fieldName, query);
            return Success(result);
        }
    }
}
