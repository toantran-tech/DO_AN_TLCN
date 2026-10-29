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
using WashGo.Domain.Entities.Partner.Interfaces;
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
        private readonly IShipperRepository _shipperRepo;
        private readonly IRepository<Role, Guid> _roleRepo;

        public WashOrderAppService(
            IWashOrderRepository orderRepo,
            ILockerRepository lockerRepo,
            IWashServiceRepository serviceRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<Customer, Guid> customerRepo,
            IShipperRepository shipperRepo,
            IRepository<Role, Guid> roleRepo)
        {
            _orderRepo = orderRepo;
            _lockerRepo = lockerRepo;
            _serviceRepo = serviceRepo;
            _userRepo = userRepo;
            _merchantRepo = merchantRepo;
            _customerRepo = customerRepo;
            _shipperRepo = shipperRepo;
            _roleRepo = roleRepo;
        }

        private readonly record struct OrderScope(string Role, Guid Id);

        private async Task<OrderScope> GetOrderScopeAsync()
        {
            if (!CurrentUser.Id.HasValue) return new("None", Guid.Empty);
            if (CurrentUser.IsInRole("Admin")) return new("Admin", CurrentUser.Id.Value);
            if (CurrentUser.IsInRole("Merchant")) return new("Merchant", CurrentUser.Id.Value);
            if (CurrentUser.IsInRole("Shipper"))
            {
                var shipper = await _shipperRepo.FirstOrDefaultAsync(x =>
                    x.UserId == CurrentUser.Id.Value && x.IsActive);
                return shipper == null ? new("None", Guid.Empty) : new("Shipper", shipper.Id);
            }
            if (CurrentUser.IsInRole("Customer")) return new("Customer", CurrentUser.Id.Value);
            return new("None", Guid.Empty);
        }

        private static IQueryable<WashOrder> ApplyScope(IQueryable<WashOrder> query, OrderScope scope) =>
            scope.Role switch
            {
                "Admin" => query,
                "Merchant" => query.Where(x => x.MerchantId == scope.Id),
                "Shipper" => query.Where(x => x.ShipperId == scope.Id),
                "Customer" => query.Where(x => x.CustomerId == scope.Id),
                _ => query.Where(x => false)
            };

        private static bool CanAccess(WashOrder order, OrderScope scope) => scope.Role switch
        {
            "Admin" => true,
            "Merchant" => order.MerchantId == scope.Id,
            "Shipper" => order.ShipperId == scope.Id,
            "Customer" => order.CustomerId == scope.Id,
            _ => false
        };

        private static T RedactStaffData<T>(T dto, OrderScope scope) where T : WashOrderResultDto
        {
            if (scope.Role is "Merchant" or "Shipper")
            {
                dto.CustomerId = Guid.Empty;
                dto.CustomerName = "***";
                dto.CustomerPhoneNumber = "***";
                dto.DepositPinCode = string.Empty;
                dto.PickupPinCode = string.Empty;
                dto.QrCodeString = string.Empty;
                if (dto is WashOrderDetailDto detail)
                {
                    detail.CustomerNote = null;
                    foreach (var entry in detail.Timeline.Where(x => x.ActorRole == "Customer"))
                    {
                        entry.ActorId = null;
                        entry.ActorName = "Khách hàng";
                        entry.Note = null;
                    }
                }
            }
            return dto;
        }

        public async Task<PaginatedResult<WashOrderResultDto>> GetListAsync(WashOrderFilterQuery query)
        {
            var scope = await GetOrderScopeAsync();
            var isStaff = scope.Role is "Merchant" or "Shipper";
            if (isStaff)
            {
                query.CustomerId = null;
                query.ColumnFilters = query.ColumnFilters.Where(x => IsStaffField(x.Field)).ToList();
                query.SortBy = query.SortBy.Where(x => IsStaffField(x.Field)).ToArray();
            }
            var (data, total) = await _orderRepo.FilterProjectedDataAsync<WashOrderResultDto>(
                filterFunc: q =>
                {
                    q = ApplyScope(q, scope);
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
                        q = q.Where(x => x.OrderCode.ToLower().Contains(kw) || !isStaff && (
                                         x.CustomerName.ToLower().Contains(kw) ||
                                         x.CustomerPhoneNumber.Contains(kw)));
                    }
                    return q;
                },
                parameters: query,
                defaultOrder: q => q.OrderByDescending(x => x.CreatedOn)
            );

            return Success(data.Select(x => RedactStaffData(x, scope)).ToList(), total, query.Page, query.Take);
        }

        public Task<PaginatedResult<WashOrderResultDto>> GetMyOrdersAsync(WashOrderFilterQuery query)
        {
            if (!CurrentUser.IsInRole("Customer"))
                return Task.FromResult(Success(Array.Empty<WashOrderResultDto>(), 0, query.Page, query.Take));
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

            var scope = await GetOrderScopeAsync();
            if (!CanAccess(order, scope))
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng của khách hàng.");
            }

            var dto = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(RedactStaffData(dto, scope));
        }

        public async Task<BaseResponse<WashOrderDetailDto>> CreateAsync(CreateWashOrderCommand command)
        {
            if (!CurrentUser.Id.HasValue)
            {
                return BadRequest<WashOrderDetailDto>("Không xác định được tài khoản khách hàng.");
            }

            if (command.Items == null || command.Items.Count is < 1 or > 30 ||
                command.Items.Any(x => x.ServiceId == Guid.Empty || x.Quantity is < 1 or > 1000 ||
                    (x.WeightKg.HasValue && (x.WeightKg.Value <= 0 || x.WeightKg.Value > 1000 ||
                        decimal.Round(x.WeightKg.Value, 2) != x.WeightKg.Value))) ||
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
            var hasWeightService = false;
            var baseProcessingHours = 0;
            foreach (var item in command.Items)
            {
                var service = await _serviceRepo.FindAsync(item.ServiceId);
                if (service == null || service.IsDeleted || !service.IsActive ||
                    service.ApprovalStatus != ServiceApprovalStatus.Approved ||
                    service.MerchantId != locker.MerchantId)
                    return BadRequest<WashOrderDetailDto>("Dịch vụ đã chọn không còn được bán bởi đối tác của tủ.");

                var isWeightBased = service.Unit.Equals("kg", StringComparison.OrdinalIgnoreCase);
                if (isWeightBased && item.Quantity != 1)
                    return BadRequest<WashOrderDetailDto>("Dịch vụ theo gói kg chỉ chọn một gói cho mỗi loại dịch vụ.");
                if (!isWeightBased && item.WeightKg.HasValue)
                    return BadRequest<WashOrderDetailDto>("Dịch vụ theo chiếc/đôi không nhận khối lượng kg.");
                if (service.EstimatedDurationHours is < 1 or > 8760)
                    return BadRequest<WashOrderDetailDto>("Thời gian xử lý dịch vụ chưa được cấu hình hợp lệ.");

                // UnitPrice is the package base price for included-weight services.
                // Estimated kilograms do not multiply this price a second time.
                var unitPrice = isWeightBased ? service.UnitPrice : service.PricePerItem ?? service.UnitPrice;
                if (unitPrice <= 0)
                    return BadRequest<WashOrderDetailDto>("Giá dịch vụ chưa được cấu hình hợp lệ.");
                var subTotal = isWeightBased ? unitPrice : unitPrice * item.Quantity;
                total += subTotal;
                hasWeightService |= isWeightBased;
                baseProcessingHours = Math.Max(baseProcessingHours, service.EstimatedDurationHours);
                order.Items.Add(new WashOrderItem
                {
                    ServiceId = service.Id,
                    ServiceName = service.Name,
                    Quantity = item.Quantity,
                    WeightKg = item.WeightKg,
                    IsWeightBased = isWeightBased,
                    UnitPrice = unitPrice,
                    SubTotal = subTotal,
                    Note = item.Note
                });
            }

            order.TotalAmount = total;
            order.FinalAmount = total;
            if (!Enum.IsDefined(box.Size))
                return BadRequest<WashOrderDetailDto>("Kích thước ô tủ chưa được cấu hình hợp lệ.");
            order.IncludedWeightKg = hasWeightService ? WashOrderWeightPricing.IncludedWeight(box.Size) : 0m;
            order.BaseProcessingHours = baseProcessingHours;
            order.ExpectedDeliveryTime = DateTimeOffset.UtcNow.AddHours(baseProcessingHours).ToUnixTimeSeconds();

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

        public async Task<BaseResponse<WashOrderDetailDto>> UpdateActualWeightAsync(Guid orderId, UpdateActualWeightCommand command)
        {
            if (command.WeightKg <= 0 || command.WeightKg > 1000 || decimal.Round(command.WeightKg, 2) != command.WeightKg)
                return BadRequest<WashOrderDetailDto>("Khối lượng phải lớn hơn 0, tối đa 1.000 kg và có tối đa 2 chữ số thập phân.");

            var scope = await GetOrderScopeAsync();
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || scope.Role != "Merchant" || !CanAccess(order, scope))
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng thuộc tiệm để cập nhật cân nặng.");
            if (order.Status != WashOrderStatus.Collected || order.PaymentStatus != PaymentStatus.Unpaid)
                return BadRequest<WashOrderDetailDto>("Chỉ cân và chốt giá khi đơn đã lấy về tiệm, chưa bắt đầu giặt và chưa thanh toán.");
            if (order.IncludedWeightKg is null || order.BaseProcessingHours is null)
                return BadRequest<WashOrderDetailDto>("Đơn cũ chưa có thông tin giá gói; không tự áp dụng quy tắc mới để đổi giá đơn này.");
            if (order.IncludedWeightKg <= 0 || !order.Items.Any(x => x.IsWeightBased))
                return BadRequest<WashOrderDetailDto>("Đơn chỉ có dịch vụ theo chiếc/đôi, không áp dụng chốt giá theo kg.");
            if (order.ActualWeightKg == command.WeightKg)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Khối lượng này đã được chốt trước đó.");
            }

            var pricing = WashOrderWeightPricing.Calculate(command.WeightKg, order.IncludedWeightKg.Value,
                order.BaseProcessingHours.Value);
            var finalAmount = order.TotalAmount + pricing.SurchargeAmount - order.DiscountAmount;
            if (finalAmount <= 0)
                return BadRequest<WashOrderDetailDto>("Giá cuối cùng không hợp lệ; chưa chốt cân nặng.");
            var now = DateTime.UtcNow;
            order.ActualWeightKg = command.WeightKg;
            order.WeightSurchargeAmount = pricing.SurchargeAmount;
            order.FinalAmount = finalAmount;
            order.EtaUpdatedAt = now;
            order.EstimatedFinishAt = now.AddHours(pricing.ProcessingHours);
            order.ExpectedDeliveryTime = new DateTimeOffset(order.EstimatedFinishAt.Value).ToUnixTimeSeconds();
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id, FromStatus = order.Status, Status = order.Status,
                ActorId = CurrentUser.Id, ActorName = CurrentUser.UserName ?? "Merchant", ActorRole = "Merchant",
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds(),
                Note = $"Chốt {command.WeightKg:0.##} kg; vượt {pricing.ExcessWeightKg:0.##} kg; phụ phí {pricing.SurchargeAmount:0.##}đ; giá cuối {finalAmount:0.##}đ. Thanh toán sau khi giặt xong."
            });
            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Đã chốt cân nặng, giá và ETA. Đơn tiếp tục xử lý giặt; khách thanh toán sau khi giặt xong.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> UpdateStatusAsync(UpdateWashOrderStatusCommand command)
        {
            if (command.NewStatus is WashOrderStatus.Paid or WashOrderStatus.Cancelled)
                return BadRequest<WashOrderDetailDto>("Thanh toán và hủy đơn phải dùng luồng nghiệp vụ riêng.");

            var scope = await GetOrderScopeAsync();
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(command.OrderId);
            var order = await _orderRepo.GetWithDetailsAsync(command.OrderId);
            if (order == null || !CanAccess(order, scope) || scope.Role == "Customer")
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng được phép cập nhật.");
            }

            if (scope.Role == "Merchant" && command.NewStatus is not
                (WashOrderStatus.Washing or WashOrderStatus.Drying or WashOrderStatus.Ironing or
                 WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment) ||
                scope.Role == "Shipper" && command.NewStatus is not
                (WashOrderStatus.Collected or WashOrderStatus.ReadyToReturn))
                return BadRequest<WashOrderDetailDto>("Vai trò hiện tại không được thực hiện bước này.");

            if (scope.Role == "Shipper" &&
                (command.NewStatus == WashOrderStatus.Collected && order.AssignedTaskType != DeliveryTaskType.Collect ||
                 command.NewStatus == WashOrderStatus.ReadyToReturn && order.AssignedTaskType != DeliveryTaskType.Return))
                return BadRequest<WashOrderDetailDto>("Shipper chưa được phân công cho nhiệm vụ giao nhận này.");

            var previousStatus = order.Status;
            if (order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue &&
                command.NewStatus is WashOrderStatus.Washing or WashOrderStatus.AwaitingPayment)
                return BadRequest<WashOrderDetailDto>("Merchant cần cập nhật số kg thực tế và chốt giá trước khi giặt hoặc yêu cầu thanh toán.");
            if (!IsValidTransition(previousStatus, command.NewStatus))
            {
                return BadRequest<WashOrderDetailDto>(
                    $"Không thể chuyển trạng thái từ {previousStatus} sang {command.NewStatus}.");
            }

            // Payment enforcement rule: ReadyToReturn, Returned, Completed require PAID
            var isReturningOrComplete = command.NewStatus is WashOrderStatus.ReadyToReturn or WashOrderStatus.Returned or WashOrderStatus.Completed;
            if (isReturningOrComplete && order.PaymentStatus != PaymentStatus.Paid)
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

            if (!await _orderRepo.TryChangeStatusAsync(order.Id, previousStatus, command.NewStatus))
                return BadRequest<WashOrderDetailDto>("Đơn hàng vừa đổi trạng thái; vui lòng tải lại trước khi cập nhật.");

            if (previousStatus == WashOrderStatus.Pending && command.NewStatus == WashOrderStatus.Deposited)
            {
                if (!await _lockerRepo.TryOccupyReservedBoxAsync(order.LockerId, order.BoxId))
                    return BadRequest<WashOrderDetailDto>("Ô tủ không còn ở trạng thái giữ chỗ; chưa nhận đồ.");
            }
            else if (previousStatus == WashOrderStatus.Deposited &&
                     command.NewStatus == WashOrderStatus.Collected)
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
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? "Nhân viên WashGo",
                ActorRole = scope.Role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = command.Note ?? $"Chuyển trạng thái từ {previousStatus} sang {command.NewStatus}"
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(RedactStaffData(result, scope), "Cập nhật trạng thái đơn thành công!");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> MoveToStorageAsync(MoveOrderToStorageCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (string.IsNullOrWhiteSpace(command.StorageLocation) || command.StorageLocation.Trim().Length > 200)
                return BadRequest<WashOrderDetailDto>("Cần nhập vị trí kho hợp lệ (tối đa 200 ký tự).");
            if (string.IsNullOrWhiteSpace(command.StorageReference) || command.StorageReference.Trim().Length > 120)
                return BadRequest<WashOrderDetailDto>("Cần nhập mã biên nhận/vị trí lưu kho (tối đa 120 ký tự).");
            if (!string.IsNullOrWhiteSpace(command.Note) && command.Note.Trim().Length > 140)
                return BadRequest<WashOrderDetailDto>("Ghi chú chuyển kho tối đa 140 ký tự.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var order = await _orderRepo.GetWithDetailsAsync(command.OrderId);
            if (order == null || !CanAccess(order, scope) || scope.Role is not ("Shipper" or "Admin"))
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng được phép chuyển kho.");
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
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope), "Đã ghi nhận chuyển đồ sang kho và giải phóng ô locker.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> AssignShipperAsync(AssignShipperCommand command)
        {
            var scope = await GetOrderScopeAsync();
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(command.OrderId);
            var order = await _orderRepo.GetWithDetailsAsync(command.OrderId);
            if (order == null || scope.Role is not ("Admin" or "Merchant") || !CanAccess(order, scope))
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng được phép phân công.");
            }

            var task = GetAssignableTask(order);
            if (!task.HasValue)
                return BadRequest<WashOrderDetailDto>("Chỉ phân công khi đơn đã gửi đồ hoặc đã thanh toán và chờ mang đồ về tủ.");

            // Serialize assignments for the same Shipper, including different orders.
            var shipper = await _shipperRepo.GetForUpdateAsync(command.ShipperId);
            if (shipper == null || shipper.MerchantId != order.MerchantId)
                return BadRequest<WashOrderDetailDto>("Shipper phải thuộc cùng tiệm với đơn hàng.");
            var user = await _userRepo.FindAsync(shipper.UserId);
            if (!shipper.IsActive || shipper.Status != ShipperStatus.Available || user == null || !user.IsActive ||
                !await _roleRepo.AnyAsync(x => x.Id == user.RoleId && x.Name == "Shipper" && x.IsActive))
                return BadRequest<WashOrderDetailDto>("Shipper không hoạt động hoặc không sẵn sàng nhận chuyến.");
            if (await AsyncExecuter.AnyAsync(ActiveDeliveryOrders(await _orderRepo.GetQueryableAsync())
                    .Where(x => x.Id != order.Id && x.ShipperId == shipper.Id)))
                return BadRequest<WashOrderDetailDto>("Shipper đang có chuyến chưa hoàn tất; vui lòng chọn người khác.");

            if (order.ShipperId == shipper.Id && order.AssignedTaskType == task)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Shipper đã được phân công cho nhiệm vụ này.");
            }

            order.ShipperId = shipper.Id;
            order.ShipperName = user.FullName;
            order.AssignedTaskType = task;
            order.ShipperAssignedAt = DateTime.UtcNow;
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                Status = order.Status,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? "Admin",
                ActorRole = scope.Role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Phân công Shipper {user.FullName}: {(task == DeliveryTaskType.Collect ? "lấy đồ" : "trả đồ")}."
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(RedactStaffData(result, scope), "Phân công Shipper thành công!");
        }

        public async Task<BaseResponse<List<AvailableShipperDto>>> GetAvailableShippersAsync(Guid orderId)
        {
            var scope = await GetOrderScopeAsync();
            var order = await _orderRepo.FindAsync(orderId);
            if (order == null || scope.Role is not ("Admin" or "Merchant") || !CanAccess(order, scope))
                return NotFound<List<AvailableShipperDto>>("Không tìm thấy đơn hàng được phép phân công.");
            var task = GetAssignableTask(order);
            if (!task.HasValue)
                return BadRequest<List<AvailableShipperDto>>("Đơn chưa ở giai đoạn phân công giao nhận.");

            var shippers = await _shipperRepo.GetQueryableAsync();
            var users = await _userRepo.GetQueryableAsync();
            var roles = await _roleRepo.GetQueryableAsync();
            var activeOrders = ActiveDeliveryOrders(await _orderRepo.GetQueryableAsync());
            var candidates = from shipper in shippers
                             join user in users on shipper.UserId equals user.Id
                             join role in roles on user.RoleId equals role.Id
                             where shipper.MerchantId == order.MerchantId && shipper.IsActive &&
                                 shipper.Status == ShipperStatus.Available && user.IsActive &&
                                 role.IsActive && role.Name == "Shipper" &&
                                 !activeOrders.Any(x => x.Id != orderId && x.ShipperId == shipper.Id)
                             orderby user.FullName, shipper.Id
                             select new AvailableShipperDto
                             {
                                 Id = shipper.Id, FullName = user.FullName, Phone = user.Phone,
                                 VehicleType = shipper.VehicleType, VehiclePlate = shipper.VehiclePlate,
                                 Rating = shipper.Rating, ActiveDeliveryCount = activeOrders.Count(x => x.ShipperId == shipper.Id),
                                 TaskType = task.Value
                             };
            return Success(await AsyncExecuter.ToListAsync(candidates));
        }

        private static DeliveryTaskType? GetAssignableTask(WashOrder order) => order.Status switch
        {
            WashOrderStatus.Deposited => DeliveryTaskType.Collect,
            WashOrderStatus.Paid when order.PaymentStatus == PaymentStatus.Paid => DeliveryTaskType.Return,
            _ => null
        };

        private static IQueryable<WashOrder> ActiveDeliveryOrders(IQueryable<WashOrder> orders) =>
            orders.Where(x => !x.IsDeleted && x.ShipperId != null &&
                (x.Status == WashOrderStatus.Deposited && x.AssignedTaskType == DeliveryTaskType.Collect ||
                 x.Status == WashOrderStatus.Paid && x.AssignedTaskType == DeliveryTaskType.Return));

        private static bool IsValidTransition(WashOrderStatus current, WashOrderStatus next)
        {
            return current switch
            {
                WashOrderStatus.Pending => next == WashOrderStatus.Deposited,
                WashOrderStatus.Deposited => next == WashOrderStatus.Collected,
                WashOrderStatus.Collected => next == WashOrderStatus.Washing,
                WashOrderStatus.Washing => next is WashOrderStatus.Drying or WashOrderStatus.Ironing or WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Drying => next is WashOrderStatus.Ironing or WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Ironing => next is WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Folding => next == WashOrderStatus.AwaitingPayment,
                WashOrderStatus.AwaitingPayment => false, // Payment gateway confirms this transition internally.
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
            var scope = await GetOrderScopeAsync();
            if (scope.Role is "None" or "Customer")
                return NotFound<List<ColumnFilterDistinctValueDto>>("Không tìm thấy dữ liệu được phép xem.");
            if ((scope.Role is "Merchant" or "Shipper") && !IsStaffField(fieldName))
                return BadRequest<List<ColumnFilterDistinctValueDto>>("Không được lọc theo thông tin riêng tư của khách hàng.");
            var result = await _orderRepo.GetColumnDistinctValuesAsync(fieldName, query,
                baseFilter: q => ApplyScope(q, scope));
            return Success(result);
        }

        private static bool IsStaffField(string field) => new[]
        {
            "OrderCode", "LockerName", "MerchantName", "ShipperName", "Status",
            "PaymentStatus", "PaymentMethod", "CreatedOn", "ExpectedDeliveryTime"
        }.Contains(field, StringComparer.OrdinalIgnoreCase);
    }
}
