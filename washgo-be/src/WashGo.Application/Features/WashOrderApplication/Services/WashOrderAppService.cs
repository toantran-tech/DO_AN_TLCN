using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Queries;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
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

        public WashOrderAppService(
            IWashOrderRepository orderRepo,
            ILockerRepository lockerRepo,
            IWashServiceRepository serviceRepo,
            IRepository<User, Guid> userRepo)
        {
            _orderRepo = orderRepo;
            _lockerRepo = lockerRepo;
            _serviceRepo = serviceRepo;
            _userRepo = userRepo;
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

        public async Task<BaseResponse<WashOrderDetailDto>> GetByIdAsync(Guid id)
        {
            var order = await _orderRepo.GetWithDetailsAsync(id);
            if (order == null)
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng!");
            }

            var dto = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(dto);
        }

        public async Task<BaseResponse<WashOrderDetailDto>> CreateAsync(CreateWashOrderCommand command)
        {
            var locker = await _lockerRepo.GetWithBoxesAsync(command.LockerId);
            if (locker == null)
            {
                return BadRequest<WashOrderDetailDto>("Không tìm thấy tủ Locker đã chọn!");
            }

            var box = locker.Boxes.FirstOrDefault(b => b.Id == command.BoxId);
            if (box == null || box.Status != LockerBoxStatus.Available)
            {
                return BadRequest<WashOrderDetailDto>("Ô tủ đã chọn không khả dụng hoặc đã có người sử dụng!");
            }

            var customerUser = await _userRepo.FindAsync(command.CustomerId);
            var customerName = !string.IsNullOrWhiteSpace(command.CustomerName)
                ? command.CustomerName
                : (customerUser?.FullName ?? "Khách hàng WashGo");
            var customerPhone = !string.IsNullOrWhiteSpace(command.CustomerPhoneNumber)
                ? command.CustomerPhoneNumber
                : (customerUser?.Phone ?? "0900000000");

            var random = new Random();
            var depositPin = random.Next(100000, 999999).ToString();
            var pickupPin = random.Next(100000, 999999).ToString();
            var orderCode = $"WG{DateTime.UtcNow:yyMMdd}{random.Next(1000, 9999)}";

            var order = new WashOrder
            {
                OrderCode = orderCode,
                CustomerId = command.CustomerId,
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
                var service = await _serviceRepo.GetAsync(item.ServiceId);
                if (service != null)
                {
                    decimal unitPrice = service.UnitPrice;
                    decimal subTotal = item.WeightKg.HasValue && service.PricePerKg.HasValue
                        ? service.PricePerKg.Value * item.WeightKg.Value
                        : unitPrice * item.Quantity;

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
            }

            order.TotalAmount = total;
            order.FinalAmount = total;

            order.Timeline.Add(new WashOrderTimeline
            {
                Status = WashOrderStatus.Pending,
                ActorId = command.CustomerId,
                ActorName = customerName,
                ActorRole = "Customer",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = "Khách hàng khởi tạo đơn qua App/Web"
            });

            box.Status = LockerBoxStatus.Reserved;
            await _orderRepo.InsertAsync(order, autoSave: true);
            await _lockerRepo.UpdateAsync(locker, autoSave: true);

            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Created(result, "Tạo đơn hàng WashGo thành công!");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> UpdateStatusAsync(UpdateWashOrderStatusCommand command)
        {
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
                    return BadRequest<WashOrderDetailDto>(
                        $"Cần thanh toán phí lưu kho còn thiếu {overdueTotal - order.StorageFeeTotal:N0}đ trước khi hoàn tất nhận đồ.");
                }
            }

            if (command.NewStatus == WashOrderStatus.Paid)
            {
                order.PaymentStatus = PaymentStatus.Paid;
            }

            // Locker box status update logic
            var locker = await _lockerRepo.GetWithBoxesAsync(order.LockerId);
            if (locker != null)
            {
                var box = locker.Boxes.FirstOrDefault(b => b.Id == order.BoxId);
                if (box != null)
                {
                    if (command.NewStatus == WashOrderStatus.Deposited)
                    {
                        box.Status = LockerBoxStatus.Occupied;
                    }
                    else if (command.NewStatus == WashOrderStatus.Collected)
                    {
                        box.Status = LockerBoxStatus.Available;
                        box.CurrentPinCode = null;
                    }
                    else if (command.NewStatus == WashOrderStatus.ReadyToReturn)
                    {
                        box.Status = LockerBoxStatus.Occupied;
                    }
                    else if (command.NewStatus is WashOrderStatus.Returned or WashOrderStatus.Completed)
                    {
                        box.Status = LockerBoxStatus.Available;
                        box.CurrentPinCode = null;
                    }
                }
                locker.AvailableBoxes = locker.Boxes.Count(b => b.Status == LockerBoxStatus.Available);
                await _lockerRepo.UpdateAsync(locker, autoSave: true);
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

            var locker = await _lockerRepo.GetWithBoxesAsync(order.LockerId);
            if (locker == null)
                return BadRequest<WashOrderDetailDto>("Không tìm thấy locker đang giữ đơn hàng.");
            var box = locker.Boxes.FirstOrDefault(x => x.Id == order.BoxId);
            if (box == null || box.Status != LockerBoxStatus.Occupied)
                return BadRequest<WashOrderDetailDto>("Ô locker của đơn không ở trạng thái đang chứa đồ.");

            var previousStatus = order.Status;
            box.Status = LockerBoxStatus.Available;
            box.CurrentPinCode = null;
            locker.AvailableBoxes = locker.Boxes.Count(x => x.Status == LockerBoxStatus.Available);
            await _lockerRepo.UpdateAsync(locker, autoSave: true);

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

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, WashOrderFilterQuery query)
        {
            var result = await _orderRepo.GetColumnDistinctValuesAsync(fieldName, query);
            return Success(result);
        }
    }
}
