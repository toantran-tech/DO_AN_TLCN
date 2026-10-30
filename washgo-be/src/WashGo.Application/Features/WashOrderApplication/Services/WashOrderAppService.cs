using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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
            if (scope.Role == "Customer")
            {
                // Customer cannot see Shipper task codes
                dto.ShipperTaskPinCode = null;
                dto.ShipperTaskQrString = null;
            }
            else if (scope.Role == "Merchant")
            {
                dto.CustomerId = Guid.Empty;
                dto.CustomerName = "***";
                dto.CustomerPhoneNumber = "***";
                dto.DepositPinCode = string.Empty;
                dto.PickupPinCode = string.Empty;
                dto.QrCodeString = string.Empty;
                dto.ShipperTaskPinCode = null;
                dto.ShipperTaskQrString = null;
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
            else if (scope.Role == "Shipper")
            {
                dto.CustomerId = Guid.Empty;
                dto.CustomerName = "***";
                dto.CustomerPhoneNumber = "***";
                dto.DepositPinCode = string.Empty;
                dto.PickupPinCode = string.Empty;
                dto.QrCodeString = string.Empty;
                // Only the assigned shipper can see their task PIN & QR
                if (scope.Id != dto.ShipperId)
                {
                    dto.ShipperTaskPinCode = null;
                    dto.ShipperTaskQrString = null;
                }
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

            // Locker credentials use cryptographically secure random numbers.
            var depositPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var pickupPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var orderCode = $"WG{DateTime.UtcNow:yyMMdd}{RandomNumberGenerator.GetInt32(1000, 10000)}";

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
                QrCodeString = $"WASHGO:DEPOSIT:{orderCode}:{depositPin}",
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
            if (command.NewStatus is WashOrderStatus.Paid or WashOrderStatus.Cancelled or
                WashOrderStatus.Deposited or WashOrderStatus.Collected or
                WashOrderStatus.ReadyToReturn or WashOrderStatus.Returned or
                WashOrderStatus.Completed or WashOrderStatus.MovedToStorage)
            {
                return BadRequest<WashOrderDetailDto>($"Trạng thái {command.NewStatus} phải được thực hiện qua luồng nghiệp vụ riêng.");
            }

            var scope = await GetOrderScopeAsync();
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(command.OrderId);
            var order = await _orderRepo.GetWithDetailsAsync(command.OrderId);
            if (order == null || !CanAccess(order, scope) || scope.Role == "Customer")
            {
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng được phép cập nhật.");
            }

            if (scope.Role is not ("Merchant" or "Admin"))
                return BadRequest<WashOrderDetailDto>("Chỉ Merchant hoặc Admin được cập nhật các bước xử lý giặt.");

            if (scope.Role == "Merchant" && command.NewStatus is not
                (WashOrderStatus.Washing or WashOrderStatus.Drying or WashOrderStatus.Ironing or
                 WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment))
                return BadRequest<WashOrderDetailDto>("Vai trò hiện tại không được thực hiện bước này.");

            var previousStatus = order.Status;
            if (order.IncludedWeightKg > 0 && !order.ActualWeightKg.HasValue &&
                command.NewStatus is WashOrderStatus.Washing or WashOrderStatus.AwaitingPayment)
                return BadRequest<WashOrderDetailDto>("Merchant cần cập nhật số kg thực tế và chốt giá trước khi giặt hoặc yêu cầu thanh toán.");
            if (!IsValidTransition(previousStatus, command.NewStatus))
            {
                return BadRequest<WashOrderDetailDto>(
                    $"Không thể chuyển trạng thái từ {previousStatus} sang {command.NewStatus}.");
            }

            if (!await _orderRepo.TryChangeStatusAsync(order.Id, previousStatus, command.NewStatus))
                return BadRequest<WashOrderDetailDto>("Đơn hàng vừa đổi trạng thái; vui lòng tải lại trước khi cập nhật.");

            order.Status = command.NewStatus;
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
            await _orderRepo.GetForUpdateAsync(command.OrderId);
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
            order.LockerOverdueFeeAtStorageTransfer = OverdueFeeCalculator.Calculate(order.PickupDeadline, now);
            order.OverdueFeeTotal = order.LockerOverdueFeeAtStorageTransfer.Value;
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

            // Generate a secure 6-digit PIN and task QR for this assignment
            var taskPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var taskTypeStr = task == DeliveryTaskType.Collect ? "COLLECT" : "RETURN";
            var taskQr = $"WASHGO:{taskTypeStr}:{order.OrderCode}:{taskPin}";

            if (order.ShipperId == shipper.Id && order.AssignedTaskType == task)
            {
                if (string.IsNullOrEmpty(order.ShipperTaskPinCode))
                {
                    order.ShipperTaskPinCode = taskPin;
                    order.ShipperTaskQrString = taskQr;
                    order.TaskPinAttemptCount = 0;
                    order.TaskPinLockoutUntil = null;
                    await _orderRepo.UpdateAsync(order, autoSave: true);
                }
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Shipper đã được phân công cho nhiệm vụ này.");
            }

            order.ShipperId = shipper.Id;
            order.ShipperName = user.FullName;
            order.AssignedTaskType = task;
            order.ShipperAssignedAt = DateTime.UtcNow;
            order.ShipperTaskPinCode = taskPin;
            order.ShipperTaskQrString = taskQr;
            order.TaskPinAttemptCount = 0;
            order.TaskPinLockoutUntil = null;

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

        #region QR/PIN Simulation Flow

        public async Task<BaseResponse<WashOrderDetailDto>> DepositAsync(Guid orderId, DepositWashOrderCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role != "Customer")
                return BadRequest<WashOrderDetailDto>("Chỉ khách hàng mới có thể thực hiện thao tác gửi đồ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || !CanAccess(order, scope) || order.CustomerId != scope.Id)
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng.");

            // Idempotency: already deposited
            if (order.Status == WashOrderStatus.Deposited)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Thao tác gửi đồ đã được thực hiện trước đó.");
            }

            if (order.Status != WashOrderStatus.Pending)
                return BadRequest<WashOrderDetailDto>("Đơn hàng không ở trạng thái chờ gửi đồ.");

            // Check 30-minute reservation window
            var orderCreatedTime = DateTimeOffset.FromUnixTimeSeconds(order.CreatedOn).UtcDateTime;
            if (DateTime.UtcNow > orderCreatedTime.AddMinutes(30))
            {
                return BadRequest<WashOrderDetailDto>("Đã quá 30 phút kể từ khi tạo đơn. Ô tủ đã hết thời gian giữ chỗ; vui lòng hủy đơn hoặc tạo đơn mới.");
            }

            // Check lockout
            var (isLocked, remainingMinutes) = CheckLockout(order.CustomerDepositPinAttemptCount, order.CustomerDepositLockoutUntil);
            if (isLocked)
            {
                return BadRequest<WashOrderDetailDto>($"Mã gửi đồ đang bị tạm khóa do nhập sai nhiều lần. Vui lòng thử lại sau {remainingMinutes} phút.");
            }

            var pinMatches = !string.IsNullOrWhiteSpace(command.PinCode) &&
                string.Equals(command.PinCode.Trim(), order.DepositPinCode, StringComparison.OrdinalIgnoreCase);
            var qrMatches = !string.IsNullOrWhiteSpace(command.QrCode) &&
                string.Equals(command.QrCode.Trim(), order.QrCodeString, StringComparison.OrdinalIgnoreCase);

            if (!pinMatches && !qrMatches)
            {
                var currentAttempts = await RecordFailedAttemptAsync(order, PinKind.CustomerDeposit);
                // Commit the failed attempt while holding the order lock; no locker action has run.
                await uow.CompleteAsync();
                return BadRequest<WashOrderDetailDto>(currentAttempts >= 5
                    ? "Nhập sai mã gửi đồ 5 lần liên tiếp. Mã tạm khóa trong 5 phút."
                    : $"Mã PIN hoặc mã QR gửi đồ không chính xác (Lần thử {currentAttempts}/5).");
            }

            if (!await _lockerRepo.TryOccupyReservedBoxAsync(order.LockerId, order.BoxId))
            {
                await uow.RollbackAsync();
                return BadRequest<WashOrderDetailDto>("Ô tủ không còn ở trạng thái giữ chỗ; vui lòng liên hệ hỗ trợ.");
            }

            var previousStatus = order.Status;
            order.Status = WashOrderStatus.Deposited;
            order.DepositedAt = DateTime.UtcNow;
            order.CustomerDepositPinAttemptCount = 0;
            order.CustomerDepositLockoutUntil = null;

            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = previousStatus,
                Status = WashOrderStatus.Deposited,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? order.CustomerName,
                ActorRole = "Customer",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = "Khách hàng đã mở tủ bằng mã PIN/QR và gửi đồ thành công."
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Gửi đồ vào tủ thành công! Đơn hàng đã chuyển sang trạng thái đã gửi đồ.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> ShipperCollectAsync(Guid orderId, ShipperCollectOrderCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role is not ("Shipper" or "Admin"))
                return BadRequest<WashOrderDetailDto>("Chỉ Shipper được phân công hoặc Admin mới có quyền lấy đồ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || !CanAccess(order, scope))
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng.");

            if (scope.Role == "Shipper")
            {
                if (order.ShipperId != scope.Id || order.AssignedTaskType != DeliveryTaskType.Collect)
                {
                    return BadRequest<WashOrderDetailDto>("Bạn chưa được phân công hoặc không có nhiệm vụ lấy đồ cho đơn này.");
                }
            }

            // Idempotency: already collected
            if (order.Status == WashOrderStatus.Collected)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Thao tác lấy đồ đã được thực hiện trước đó.");
            }

            if (order.Status != WashOrderStatus.Deposited)
                return BadRequest<WashOrderDetailDto>("Đơn hàng không ở trạng thái đã gửi đồ vào tủ.");

            if (!order.ShipperId.HasValue || order.AssignedTaskType != DeliveryTaskType.Collect)
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa được phân công chuyến lấy đồ.");

            // Check lockout
            var (isLocked, remainingMinutes) = CheckLockout(order.TaskPinAttemptCount, order.TaskPinLockoutUntil);
            if (isLocked)
            {
                return BadRequest<WashOrderDetailDto>($"Mã nhiệm vụ đang bị tạm khóa do nhập sai nhiều lần. Vui lòng thử lại sau {remainingMinutes} phút.");
            }

            var pinMatches = !string.IsNullOrWhiteSpace(command.PinCode) &&
                string.Equals(command.PinCode.Trim(), order.ShipperTaskPinCode, StringComparison.OrdinalIgnoreCase);
            var qrMatches = !string.IsNullOrWhiteSpace(command.QrCode) &&
                string.Equals(command.QrCode.Trim(), order.ShipperTaskQrString, StringComparison.OrdinalIgnoreCase);

            if (!pinMatches && !qrMatches)
            {
                var currentAttempts = await RecordFailedAttemptAsync(order, PinKind.ShipperTask);
                // Commit the failed attempt while holding the order lock; no locker action has run.
                await uow.CompleteAsync();
                return BadRequest<WashOrderDetailDto>(currentAttempts >= 5
                    ? "Nhập sai mã nhiệm vụ 5 lần liên tiếp. Mã tạm khóa trong 5 phút."
                    : $"Mã nhiệm vụ không chính xác (Lần thử {currentAttempts}/5).");
            }

            if (!await _lockerRepo.TryReleaseOccupiedBoxAsync(order.LockerId, order.BoxId))
            {
                await uow.RollbackAsync();
                return BadRequest<WashOrderDetailDto>("Ô gửi đồ không còn ở trạng thái chứa đồ; chưa đổi trạng thái đơn.");
            }

            // SLA Check: 4 hours from deposited time
            var depositedAt = order.DepositedAt ?? DateTime.UtcNow;
            var elapsed = DateTime.UtcNow - depositedAt;
            var isOverdueSla = elapsed > TimeSpan.FromHours(4);
            var slaWarning = isOverdueSla ? $" (Cảnh báo: Trễ SLA lấy đồ {elapsed.TotalHours:0.#} giờ so với cam kết 4 giờ)" : string.Empty;

            var previousStatus = order.Status;
            order.Status = WashOrderStatus.Collected;
            order.TaskPinAttemptCount = 0;
            order.TaskPinLockoutUntil = null;

            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = previousStatus,
                Status = WashOrderStatus.Collected,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? (scope.Role == "Admin" ? "Admin" : order.ShipperName ?? "Shipper"),
                ActorRole = scope.Role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Shipper đã mở ô tủ và lấy đồ về tiệm thành công.{slaWarning}"
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Shipper lấy đồ thành công! Đơn hàng đã chuyển sang trạng thái đã thu gom.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> ShipperReturnAsync(Guid orderId, ShipperReturnOrderCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role is not ("Shipper" or "Admin"))
                return BadRequest<WashOrderDetailDto>("Chỉ Shipper được phân công hoặc Admin mới có quyền trả đồ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || !CanAccess(order, scope))
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng.");

            if (scope.Role == "Shipper")
            {
                if (order.ShipperId != scope.Id || order.AssignedTaskType != DeliveryTaskType.Return)
                {
                    return BadRequest<WashOrderDetailDto>("Bạn chưa được phân công hoặc không có nhiệm vụ trả đồ cho đơn này.");
                }
            }

            if (order.PaymentStatus != PaymentStatus.Paid)
            {
                return BadRequest<WashOrderDetailDto>("Đơn hàng phải được thanh toán (PAID) trước khi Shipper trả đồ vào tủ.");
            }

            // Idempotency: already ready to return
            if (order.Status == WashOrderStatus.ReadyToReturn)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Thao tác trả đồ đã được thực hiện trước đó.");
            }

            if (order.Status != WashOrderStatus.Paid)
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa ở trạng thái sẵn sàng để trả về tủ (phải là Paid).");

            if (!order.ShipperId.HasValue || order.AssignedTaskType != DeliveryTaskType.Return)
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa được phân công chuyến trả đồ.");

            // Check lockout
            var (isLocked, remainingMinutes) = CheckLockout(order.TaskPinAttemptCount, order.TaskPinLockoutUntil);
            if (isLocked)
            {
                return BadRequest<WashOrderDetailDto>($"Mã nhiệm vụ đang bị tạm khóa do nhập sai nhiều lần. Vui lòng thử lại sau {remainingMinutes} phút.");
            }

            var pinMatches = !string.IsNullOrWhiteSpace(command.PinCode) &&
                string.Equals(command.PinCode.Trim(), order.ShipperTaskPinCode, StringComparison.OrdinalIgnoreCase);
            var qrMatches = !string.IsNullOrWhiteSpace(command.QrCode) &&
                string.Equals(command.QrCode.Trim(), order.ShipperTaskQrString, StringComparison.OrdinalIgnoreCase);

            if (!pinMatches && !qrMatches)
            {
                var currentAttempts = await RecordFailedAttemptAsync(order, PinKind.ShipperTask);
                // Commit the failed attempt while holding the order lock; no locker action has run.
                await uow.CompleteAsync();
                return BadRequest<WashOrderDetailDto>(currentAttempts >= 5
                    ? "Nhập sai mã nhiệm vụ 5 lần liên tiếp. Mã tạm khóa trong 5 phút."
                    : $"Mã nhiệm vụ không chính xác (Lần thử {currentAttempts}/5).");
            }

            var returnLockerId = order.ReturnLockerId ?? order.LockerId;
            var returnLocker = await _lockerRepo.GetWithBoxesAsync(returnLockerId);
            if (returnLocker == null || returnLocker.Status != LockerStatus.Active || returnLocker.MerchantId != order.MerchantId)
            {
                await uow.RollbackAsync();
                return BadRequest<WashOrderDetailDto>("Tủ trả đồ không còn hoạt động hoặc không thuộc đối tác của đơn.");
            }

            var sendLocker = returnLockerId == order.LockerId
                ? returnLocker
                : await _lockerRepo.GetWithBoxesAsync(order.LockerId);
            var originalBox = sendLocker?.Boxes.FirstOrDefault(x => x.Id == order.BoxId);
            var requiredSize = originalBox?.Size ?? LockerBoxSize.Small;

            LockerBox? returnBox = null;
            var candidates = returnLocker.Boxes
                .Where(x => !x.IsDeleted && x.Status == LockerBoxStatus.Available && x.Size >= requiredSize)
                .OrderBy(x => x.Size)
                .ThenBy(x => x.SlotNumber);

            foreach (var candidate in candidates)
            {
                if (!await _lockerRepo.TryReserveBoxAsync(returnLockerId, candidate.Id))
                    continue;
                if (!await _lockerRepo.TryOccupyReservedBoxAsync(returnLockerId, candidate.Id))
                {
                    await uow.RollbackAsync();
                    return BadRequest<WashOrderDetailDto>("Không thể kích hoạt ô trả đồ; vui lòng thử lại.");
                }
                returnBox = candidate;
                break;
            }

            if (returnBox == null)
            {
                await uow.RollbackAsync();
                return BadRequest<WashOrderDetailDto>("Tủ trả đồ hiện không có ô trống phù hợp kích thước; chưa thể trả đồ.");
            }

            // SLA Check: 24 hours from payment time
            var paidAt = order.PaidAt ?? DateTime.UtcNow;
            var elapsedReturn = DateTime.UtcNow - paidAt;
            var isOverdueReturn = elapsedReturn > TimeSpan.FromHours(24);
            var slaReturnWarning = isOverdueReturn ? $" (Cảnh báo: Trễ SLA trả đồ {elapsedReturn.TotalHours:0.#} giờ so với cam kết 24 giờ)" : string.Empty;

            var previousStatus = order.Status;
            order.ReturnLockerId = returnLockerId;
            order.ReturnBoxId = returnBox.Id;
            order.ReturnBoxNumber = returnBox.BoxNumber;
            order.Status = WashOrderStatus.ReadyToReturn;
            order.PickupDeadline = DateTime.UtcNow.AddHours(24);
            order.TaskPinAttemptCount = 0;
            order.TaskPinLockoutUntil = null;

            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = previousStatus,
                Status = WashOrderStatus.ReadyToReturn,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? (scope.Role == "Admin" ? "Admin" : order.ShipperName ?? "Shipper"),
                ActorRole = scope.Role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Đã đưa đồ sạch vào ô số {returnBox.BoxNumber} tại tủ {returnLocker.Name}. Khách có 24h để lấy đồ.{slaReturnWarning}"
            });

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Shipper trả đồ vào tủ thành công! Đơn hàng đã chuyển sang trạng thái chờ khách nhận.");
        }

        public async Task<BaseResponse<OpenPickupBoxResultDto>> OpenPickupBoxAsync(Guid orderId, OpenPickupBoxCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role != "Customer")
                return BadRequest<OpenPickupBoxResultDto>("Chỉ khách hàng mới có thể mở tủ nhận đồ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || !CanAccess(order, scope) || order.CustomerId != scope.Id)
                return NotFound<OpenPickupBoxResultDto>("Không tìm thấy đơn hàng.");

            if (order.Status != WashOrderStatus.ReadyToReturn)
                return BadRequest<OpenPickupBoxResultDto>("Đơn hàng không ở trạng thái sẵn sàng nhận đồ tại tủ.");

            if (order.PaymentStatus != PaymentStatus.Paid)
                return BadRequest<OpenPickupBoxResultDto>("Đơn hàng chưa được thanh toán.");

            if (order.PickupDeadline.HasValue && OverdueFeeCalculator.IsTransferToStorageDue(order.PickupDeadline, DateTime.UtcNow))
            {
                return BadRequest<OpenPickupBoxResultDto>("Đơn hàng đã quá hạn 72 giờ và chuyển sang quy trình lưu kho. Vui lòng liên hệ hỗ trợ hoặc nhận đồ tại kho.");
            }

            var overdueFee = OverdueFeeCalculator.Calculate(order.PickupDeadline, DateTime.UtcNow);
            order.OverdueFeeTotal = overdueFee;
            if (order.StorageFeeTotal < overdueFee)
            {
                await _orderRepo.UpdateAsync(order, autoSave: true);
                await uow.CompleteAsync();
                return BadRequest<OpenPickupBoxResultDto>($"Cần thanh toán phí quá hạn còn thiếu {overdueFee - order.StorageFeeTotal:N0}đ trước khi mở tủ nhận đồ.");
            }

            // Check lockout
            var (isLocked, remainingMinutes) = CheckLockout(order.CustomerPickupPinAttemptCount, order.CustomerPickupLockoutUntil);
            if (isLocked)
            {
                return BadRequest<OpenPickupBoxResultDto>($"Mã nhận đồ đang bị tạm khóa do nhập sai nhiều lần. Vui lòng thử lại sau {remainingMinutes} phút.");
            }

            var pinMatches = !string.IsNullOrWhiteSpace(command.PinCode) &&
                string.Equals(command.PinCode.Trim(), order.PickupPinCode, StringComparison.OrdinalIgnoreCase);
            var qrMatches = !string.IsNullOrWhiteSpace(command.QrCode) &&
                string.Equals(command.QrCode.Trim(), $"WASHGO:PICKUP:{order.OrderCode}:{order.PickupPinCode}", StringComparison.Ordinal);

            if (!pinMatches && !qrMatches)
            {
                var currentAttempts = await RecordFailedAttemptAsync(order, PinKind.CustomerPickup);
                // Commit the failed attempt while holding the order lock; no locker action has run.
                await uow.CompleteAsync();
                return BadRequest<OpenPickupBoxResultDto>(currentAttempts >= 5
                    ? "Nhập sai mã nhận đồ 5 lần liên tiếp. Mã tạm khóa trong 5 phút."
                    : $"Mã PIN hoặc mã QR nhận đồ không chính xác (Lần thử {currentAttempts}/5).");
            }

            // Generate secure session token (32 bytes = 64 hex chars)
            var tokenBytes = new byte[32];
            RandomNumberGenerator.Fill(tokenBytes);
            var sessionToken = Convert.ToHexString(tokenBytes);
            var tokenHash = HashToken(sessionToken);

            order.PickupSessionTokenHash = tokenHash;
            order.PickupSessionExpiresAt = DateTime.UtcNow.AddMinutes(10);
            order.CustomerPickupPinAttemptCount = 0;
            order.CustomerPickupLockoutUntil = null;

            var returnLocker = await _lockerRepo.FindAsync(order.ReturnLockerId ?? order.LockerId);
            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();
            var result = new OpenPickupBoxResultDto
            {
                OrderId = order.Id,
                OrderCode = order.OrderCode,
                ReturnLockerId = order.ReturnLockerId ?? order.LockerId,
                ReturnLockerName = returnLocker?.Name ?? order.LockerName,
                ReturnBoxId = order.ReturnBoxId ?? order.BoxId,
                ReturnBoxNumber = order.ReturnBoxNumber ?? order.BoxNumber,
                PickupSessionToken = sessionToken,
                ExpiresAt = order.PickupSessionExpiresAt.Value
            };

            return Success(result, $"Cửa ô tủ số {result.ReturnBoxNumber} đã mở! Vui lòng lấy đồ và xác nhận hoàn tất.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> ConfirmPickupAsync(Guid orderId, ConfirmPickupCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role != "Customer")
                return BadRequest<WashOrderDetailDto>("Chỉ khách hàng mới có thể xác nhận nhận đồ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || !CanAccess(order, scope) || order.CustomerId != scope.Id)
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng.");

            // Idempotency: already completed or returned
            if (order.Status is WashOrderStatus.Returned or WashOrderStatus.Completed)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Đơn hàng đã được xác nhận nhận đồ trước đó.");
            }

            if (order.PaymentStatus != PaymentStatus.Paid)
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa được thanh toán tiền giặt.");

            if (order.Status != WashOrderStatus.ReadyToReturn)
                return BadRequest<WashOrderDetailDto>("Đơn hàng không ở trạng thái sẵn sàng nhận đồ tại tủ.");

            if (string.IsNullOrWhiteSpace(command.PickupSessionToken))
                return BadRequest<WashOrderDetailDto>("Thiếu mã phiên lấy đồ (PickupSessionToken).");

            var tokenHash = HashToken(command.PickupSessionToken.Trim());
            if (order.PickupSessionTokenHash == null ||
                !string.Equals(order.PickupSessionTokenHash, tokenHash, StringComparison.OrdinalIgnoreCase) ||
                !order.PickupSessionExpiresAt.HasValue ||
                DateTime.UtcNow > order.PickupSessionExpiresAt.Value)
            {
                return BadRequest<WashOrderDetailDto>("Phiên lấy đồ không hợp lệ hoặc đã hết hạn (10 phút). Vui lòng quét mã mở tủ lại.");
            }

            var overdueFee = OverdueFeeCalculator.Calculate(order.PickupDeadline, DateTime.UtcNow);
            order.OverdueFeeTotal = overdueFee;
            if (order.StorageFeeTotal < overdueFee)
            {
                return BadRequest<WashOrderDetailDto>($"Phát sinh phí quá hạn bổ sung {overdueFee - order.StorageFeeTotal:N0}đ; vui lòng thanh toán trước khi đóng phiên.");
            }

            var (returnLockerId, returnBoxId) = GetReturnSlot(order);
            if (!await _lockerRepo.TryReleaseOccupiedBoxAsync(returnLockerId, returnBoxId))
            {
                await uow.RollbackAsync();
                return BadRequest<WashOrderDetailDto>("Ô trả đồ không còn ở trạng thái chứa đồ; chưa thể hoàn tất đơn.");
            }

            order.PickupSessionTokenHash = null;
            order.PickupSessionExpiresAt = null;

            var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = WashOrderStatus.ReadyToReturn,
                Status = WashOrderStatus.Returned,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? order.CustomerName,
                ActorRole = "Customer",
                Timestamp = nowTs,
                Note = "Khách hàng đã mở tủ và lấy đồ thành công."
            });

            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = WashOrderStatus.Returned,
                Status = WashOrderStatus.Completed,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? order.CustomerName,
                ActorRole = "Customer",
                Timestamp = nowTs + 1,
                Note = "Đơn hàng hoàn tất."
            });

            order.Status = WashOrderStatus.Completed;
            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();

            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Xác nhận nhận đồ thành công! Cảm ơn bạn đã sử dụng dịch vụ WashGo.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> PickupFromStorageAsync(Guid orderId, PickupFromStorageCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role != "Customer")
                return BadRequest<WashOrderDetailDto>("Chỉ khách hàng mới có thể xác nhận nhận đồ tại kho.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || !CanAccess(order, scope) || order.CustomerId != scope.Id)
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng.");

            // Idempotency: already completed or returned
            if (order.Status is WashOrderStatus.Returned or WashOrderStatus.Completed)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Đơn hàng đã được hoàn tất trước đó.");
            }

            if (order.PaymentStatus != PaymentStatus.Paid)
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa được thanh toán tiền giặt.");

            if (order.Status != WashOrderStatus.MovedToStorage)
                return BadRequest<WashOrderDetailDto>("Đơn hàng không ở trạng thái lưu kho.");

            var now = DateTime.UtcNow;
            var totalDue = OverdueFeeCalculator.GetTotalStorageDue(order, now);
            order.OverdueFeeTotal = totalDue;
            if (order.StorageFeeTotal < totalDue)
            {
                await _orderRepo.UpdateAsync(order, autoSave: true);
                await uow.CompleteAsync();
                return BadRequest<WashOrderDetailDto>($"Cần thanh toán đủ phí lưu kho còn thiếu {totalDue - order.StorageFeeTotal:N0}đ trước khi nhận đồ tại kho.");
            }

            // Locker slot was already released during MoveToStorage; do NOT release again.
            var nowTs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = WashOrderStatus.MovedToStorage,
                Status = WashOrderStatus.Returned,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? order.CustomerName,
                ActorRole = "Customer",
                Timestamp = nowTs,
                Note = command.Note?.Trim() is { Length: > 0 } note
                    ? $"Khách hàng đã nhận lại đồ tại kho {order.StorageLocation}. {note}"
                    : $"Khách hàng đã nhận lại đồ tại kho {order.StorageLocation}."
            });

            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id,
                FromStatus = WashOrderStatus.Returned,
                Status = WashOrderStatus.Completed,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? order.CustomerName,
                ActorRole = "Customer",
                Timestamp = nowTs + 1,
                Note = "Đơn hàng hoàn tất."
            });

            order.Status = WashOrderStatus.Completed;
            await _orderRepo.UpdateAsync(order, autoSave: true);
            await uow.CompleteAsync();

            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Đã xác nhận nhận đồ tại kho thành công! Đơn hàng hoàn tất.");
        }

        #endregion

        #region Helpers & Validations

        private static string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes);
        }

        private enum PinKind
        {
            CustomerDeposit,
            ShipperTask,
            CustomerPickup
        }

        // Called inside the action's transaction with the order row locked.
        // Failed validation deliberately commits only its attempt counter before returning an error.
        private async Task<int> RecordFailedAttemptAsync(WashOrder order, PinKind kind)
        {
            var now = DateTime.UtcNow;
            int attempts;
            switch (kind)
            {
                case PinKind.CustomerDeposit:
                    if (order.CustomerDepositLockoutUntil <= now)
                    {
                        order.CustomerDepositPinAttemptCount = 0;
                        order.CustomerDepositLockoutUntil = null;
                    }
                    attempts = ++order.CustomerDepositPinAttemptCount;
                    if (attempts >= 5) order.CustomerDepositLockoutUntil = now.AddMinutes(5);
                    break;
                case PinKind.ShipperTask:
                    if (order.TaskPinLockoutUntil <= now)
                    {
                        order.TaskPinAttemptCount = 0;
                        order.TaskPinLockoutUntil = null;
                    }
                    attempts = ++order.TaskPinAttemptCount;
                    if (attempts >= 5) order.TaskPinLockoutUntil = now.AddMinutes(5);
                    break;
                default:
                    if (order.CustomerPickupLockoutUntil <= now)
                    {
                        order.CustomerPickupPinAttemptCount = 0;
                        order.CustomerPickupLockoutUntil = null;
                    }
                    attempts = ++order.CustomerPickupPinAttemptCount;
                    if (attempts >= 5) order.CustomerPickupLockoutUntil = now.AddMinutes(5);
                    break;
            }
            await _orderRepo.UpdateAsync(order, autoSave: true);
            return attempts;
        }
        private static (bool IsLocked, int RemainingMinutes) CheckLockout(int attempts, DateTime? lockoutUntil)
        {
            if (lockoutUntil.HasValue)
            {
                var now = DateTime.UtcNow;
                if (now < lockoutUntil.Value)
                {
                    var remaining = (int)Math.Ceiling((lockoutUntil.Value - now).TotalMinutes);
                    return (true, Math.Max(1, remaining));
                }
            }
            return (false, 0);
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
                WashOrderStatus.Collected => next == WashOrderStatus.Washing,
                WashOrderStatus.Washing => next is WashOrderStatus.Drying or WashOrderStatus.Ironing or WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Drying => next is WashOrderStatus.Ironing or WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Ironing => next is WashOrderStatus.Folding or WashOrderStatus.AwaitingPayment,
                WashOrderStatus.Folding => next == WashOrderStatus.AwaitingPayment,
                _ => false
            };
        }

        private static (Guid LockerId, Guid BoxId) GetReturnSlot(WashOrder order) =>
            order.ReturnBoxId.HasValue
                ? (order.ReturnLockerId ?? order.LockerId, order.ReturnBoxId.Value)
                : (order.LockerId, order.BoxId);

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

        #endregion
    }
}
