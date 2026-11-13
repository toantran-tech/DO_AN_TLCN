using WashGo.Application.Features.AuditApplication.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.Auditing;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;
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
using WashGo.Application.Contracts.Features.PromotionContracts.Results;
using WashGo.Application.Features.LockerApplication.Services;
using WashGo.Application.Features.NotificationApplication.Services;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Application.Features.ShipperApplication.Services;
using WashGo.Domain.Entities.Promotion;
using WashGo.Domain.Entities.Promotion.Interfaces;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Entities.Payment;
using WashGo.Domain.Entities.Payment.Interfaces;
using PromotionEntity = global::WashGo.Domain.Entities.Promotion.Promotion;
using System.Globalization;
using System.Net;

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
        private readonly IPaymentTransactionRepository _payments;
        private readonly IRepository<RefundRequest, Guid> _refunds;
        private readonly IPromotionRepository _promotionRepo;
        private readonly IRepository<PromotionUsage, Guid> _promotionUsageRepo;
        private readonly NotificationPublisher _notifications;
        private readonly SystemSettingProvider _settings;
        private readonly OrderListEnricher _enricher;
        private readonly EtaPredictor _eta;
        private readonly LockerMerchantResolver _lockerMerchants;
        private readonly OrderWriteGuard _writeGuard;
        private readonly DeliveryTaskRecorder _taskRecorder;
        private readonly OrderMediaEnricher _media;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly ShipperDeliveryGuard _deliveryGuard;

        public WashOrderAppService(
            IWashOrderRepository orderRepo,
            ILockerRepository lockerRepo,
            IWashServiceRepository serviceRepo,
            IRepository<User, Guid> userRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<Customer, Guid> customerRepo,
            IShipperRepository shipperRepo,
            IRepository<Role, Guid> roleRepo,
            IPaymentTransactionRepository payments,
            IRepository<RefundRequest, Guid> refunds,
            IPromotionRepository promotionRepo,
            IRepository<PromotionUsage, Guid> promotionUsageRepo,
            NotificationPublisher notifications,
            SystemSettingProvider settings,
            OrderListEnricher enricher,
            EtaPredictor eta,
            LockerMerchantResolver lockerMerchants,
            OrderWriteGuard writeGuard,
            DeliveryTaskRecorder taskRecorder,
            OrderMediaEnricher media,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            ShipperDeliveryGuard deliveryGuard)
        {
            _deliveryGuard = deliveryGuard;
            _orderRepo = orderRepo;
            _lockerRepo = lockerRepo;
            _serviceRepo = serviceRepo;
            _userRepo = userRepo;
            _merchantRepo = merchantRepo;
            _customerRepo = customerRepo;
            _shipperRepo = shipperRepo;
            _roleRepo = roleRepo;
            _payments = payments;
            _refunds = refunds;
            _promotionRepo = promotionRepo;
            _promotionUsageRepo = promotionUsageRepo;
            _notifications = notifications;
            _settings = settings;
            _enricher = enricher;
            _eta = eta;
            _lockerMerchants = lockerMerchants;
            _writeGuard = writeGuard;
            _taskRecorder = taskRecorder;
            _media = media;
            _timelineRepo = timelineRepo;
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
            if (scope.Role == "Admin")
            {
                // Admin screens do not need locker/task codes; support reads them via GET /wash-order/{id}/access-codes.
                dto.DepositPinCode = string.Empty;
                dto.PickupPinCode = string.Empty;
                dto.QrCodeString = string.Empty;
                dto.ShipperTaskPinCode = null;
                dto.ShipperTaskQrString = null;
            }
            else if (scope.Role == "Customer")
            {
                // Customer cannot see Shipper task codes
                dto.ShipperTaskPinCode = null;
                dto.ShipperTaskQrString = null;
                dto.ShipperNote = null;
            }
            else if (scope.Role == "Merchant")
            {
                dto.CancellationReason = null;
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
                dto.CancellationReason = null;
                dto.CustomerId = Guid.Empty;
                dto.CustomerName = "***";
                dto.CustomerPhoneNumber = "***";
                dto.DepositPinCode = string.Empty;
                dto.PickupPinCode = string.Empty;
                dto.QrCodeString = string.Empty;
                // Credentials are released only by the scoped task credential endpoint.
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
            return dto;
        }

        // Shared by the list and the summary: scope first, then the list filters (status only for the list).
        private static IQueryable<WashOrder> ApplyListFilters(IQueryable<WashOrder> q, WashOrderFilterQuery query,
            OrderScope scope, bool includeStatus)
        {
            var isStaff = scope.Role is "Merchant" or "Shipper";
            // WashOrder uses the project's own ISoftDelete (no global filter): exclude deleted orders explicitly.
            q = ApplyScope(q.Where(x => !x.IsDeleted), scope);
            if (includeStatus && query.Status.HasValue)
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
            if (query.TaskType.HasValue)
                q = q.Where(x => x.AssignedTaskType == query.TaskType.Value);
            if (query.PendingOnly == true)
                q = DeliveryTaskRules.ActiveDeliveries(q);
            if (!string.IsNullOrWhiteSpace(query.OverdueType))
            {
                var now = DateTime.UtcNow;
                q = query.OverdueType.Trim().ToLowerInvariant() switch
                {
                    "depositexpired" => q.Where(x => x.Status == WashOrderStatus.Pending && x.DepositDeadline != null && x.DepositDeadline < now),
                    "paymentoverdue" => q.Where(x => x.Status == WashOrderStatus.AwaitingPayment && x.PaymentStatus == PaymentStatus.Unpaid &&
                                                     x.PaymentDeadline != null && x.PaymentDeadline < now),
                    "pickupoverdue" => q.Where(x => x.Status == WashOrderStatus.ReadyToReturn && x.PickupDeadline != null && x.PickupDeadline < now),
                    _ => q.Where(x => false)
                };
            }
            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                q = q.Where(x => x.OrderCode.ToLower().Contains(kw) ||
                                 x.MerchantOrderCode != null && x.MerchantOrderCode.ToLower().Contains(kw) || !isStaff && (
                                 x.CustomerName.ToLower().Contains(kw) ||
                                 x.CustomerPhoneNumber.Contains(kw)));
            }
            return q;
        }

        private static void RestrictStaffQuery(WashOrderFilterQuery query, OrderScope scope)
        {
            if (scope.Role is not ("Merchant" or "Shipper"))
                return;
            query.CustomerId = null;
            query.ColumnFilters = query.ColumnFilters.Where(x => IsStaffField(x.Field)).ToList();
            query.SortBy = query.SortBy.Where(x => IsStaffField(x.Field)).ToArray();
        }

        public async Task<PaginatedResult<WashOrderResultDto>> GetListAsync(WashOrderFilterQuery query)
        {
            var scope = await GetOrderScopeAsync();
            if (query.DateRangeError() is { } dateError)
                return new PaginatedResult<WashOrderResultDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = dateError,
                    CurrentPage = query.Page, PageSize = query.Take
                };
            RestrictStaffQuery(query, scope);
            var (data, total) = await _orderRepo.FilterProjectedDataAsync<WashOrderResultDto>(
                filterFunc: q => ApplyListFilters(q, query, scope, includeStatus: true),
                parameters: query,
                defaultOrder: q => q.OrderByDescending(x => x.CreatedOn)
            );

            var list = data.Select(x => RedactStaffData(x, scope)).ToList();
            await _enricher.FillAsync(list, scope.Role);
            await _media.FillAsync(list, scope.Role);
            await LazyServiceProvider.LazyGetRequiredService<OrderFinanceEnricher>().FillAsync(list, scope.Role);
            return Success(list, total, query.Page, query.Take, total > 0 ? null : scope.Role switch
            {
                "Shipper" => "Bạn chưa có đơn hàng nào được phân công.",
                "Customer" or "Merchant" => "Bạn chưa có đơn hàng nào.",
                _ => "Chưa có đơn hàng nào."
            });
        }

        public Task<PaginatedResult<WashOrderResultDto>> GetMyOrdersAsync(WashOrderFilterQuery query)
        {
            if (!CurrentUser.IsInRole("Customer"))
                return Task.FromResult(Success(Array.Empty<WashOrderResultDto>(), 0, query.Page, query.Take));
            query.CustomerId = CurrentUser.Id ?? Guid.Empty;
            return GetListAsync(query);
        }

        public async Task<BaseResponse<WashOrderSummaryDto>> GetSummaryAsync(WashOrderFilterQuery query)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role is "None" or "Customer")
                return NotFound<WashOrderSummaryDto>("Không tìm thấy dữ liệu được phép xem.");
            if (query.DateRangeError() is { } dateError)
                return BadRequest<WashOrderSummaryDto>(dateError);
            RestrictStaffQuery(query, scope);
            var q = ApplyListFilters(await _orderRepo.GetQueryableAsync(), query, scope, includeStatus: false);
            if (query.FromDate > 0)
                q = q.Where(x => x.CreatedOn >= query.FromDate);
            if (query.ToDate > 0)
                q = q.Where(x => x.CreatedOn <= query.ToDate);

            // One grouped query: the total and every status count come from the same snapshot.
            var rows = await AsyncExecuter.ToListAsync(q.GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() }));
            var counts = rows.ToDictionary(x => x.Status, x => x.Count);
            int C(WashOrderStatus status) => counts.GetValueOrDefault(status);
            return Success(new WashOrderSummaryDto
            {
                Total = rows.Sum(x => x.Count),
                StatusCounts = new MerchantStatusCountsDto
                {
                    Pending = C(WashOrderStatus.Pending), Deposited = C(WashOrderStatus.Deposited),
                    Collected = C(WashOrderStatus.Collected), Washing = C(WashOrderStatus.Washing),
                    Drying = C(WashOrderStatus.Drying), Ironing = C(WashOrderStatus.Ironing),
                    Folding = C(WashOrderStatus.Folding), AwaitingPayment = C(WashOrderStatus.AwaitingPayment),
                    Paid = C(WashOrderStatus.Paid), ReadyToReturn = C(WashOrderStatus.ReadyToReturn),
                    Returned = C(WashOrderStatus.Returned), Completed = C(WashOrderStatus.Completed),
                    MovedToStorage = C(WashOrderStatus.MovedToStorage), Cancelled = C(WashOrderStatus.Cancelled)
                },
                FromDate = query.FromDate,
                ToDate = query.ToDate,
                GeneratedAt = DateTime.UtcNow
            });
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
            await _enricher.FillAsync([dto], scope.Role);
            await _enricher.FillLockersAsync(dto);
            await _eta.FillAsync(dto);
            if (scope.Role is "Shipper" or "Merchant" or "Admin")
                await _enricher.FillTaskAsync(dto);
            dto.AllowedActions = OrderActionRules.Build(order, scope.Role);
            await _media.FillAsync([dto], scope.Role);
            await LazyServiceProvider.LazyGetRequiredService<OrderFinanceEnricher>().FillAsync([dto], scope.Role);
            return Success(RedactStaffData(dto, scope));
        }

        public async Task<BaseResponse<OrderAccessCodesDto>> GetAccessCodesAsync(Guid id)
        {
            var scope = await GetOrderScopeAsync();
            var order = await _orderRepo.FindAsync(id);
            if (order == null || order.IsDeleted || scope.Role != "Admin")
                return NotFound<OrderAccessCodesDto>("Không tìm thấy đơn hàng!");
            // Every reveal is recorded on the order history (who and when, never the codes themselves).
            await _timelineRepo.InsertAsync(new WashOrderTimeline
            {
                WashOrderId = order.Id, FromStatus = order.Status, Status = order.Status,
                ActorId = CurrentUser.Id, ActorName = CurrentUser.UserName ?? "Admin", ActorRole = "Admin",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = "Admin xem mã mở tủ/mã nhiệm vụ để hỗ trợ."
            }, autoSave: true);
            await Audit.LogAsync(new AuditEntry("Order", "RevealAccessCodes", "Order", order.Id.ToString(), order.OrderCode));
            return Success(new OrderAccessCodesDto
            {
                OrderId = order.Id, OrderCode = order.OrderCode,
                DepositPinCode = order.DepositPinCode, QrCodeString = order.QrCodeString,
                PickupPinCode = order.PickupPinCode,
                PickupQrCodeString = string.IsNullOrEmpty(order.PickupPinCode) ? null : $"WASHGO:PICKUP:{order.OrderCode}:{order.PickupPinCode}",
                ShipperTaskPinCode = order.ShipperTaskPinCode, ShipperTaskQrString = order.ShipperTaskQrString
            });
        }

        private sealed record PricedOrder(List<WashOrderItem> Items, decimal Total, bool HasWeightService, int BaseProcessingHours);

        private static string? ValidateItems(List<CreateWashOrderItemDto>? items) =>
            items == null || items.Count is < 1 or > 30 ||
            items.Any(x => x.ServiceId == Guid.Empty || x.Quantity is < 1 or > 1000 ||
                (x.WeightKg.HasValue && (x.WeightKg.Value <= 0 || x.WeightKg.Value > 1000 ||
                    decimal.Round(x.WeightKg.Value, 2) != x.WeightKg.Value))) ||
            items.Select(x => x.ServiceId).Distinct().Count() != items.Count
                ? "Cần chọn 1–30 dịch vụ khác nhau; số lượng và khối lượng phải hợp lệ."
                : null;

        // Prices always come from the server catalog; client prices are never trusted.
        private async Task<(PricedOrder? Result, string? Error)> PriceItemsAsync(List<CreateWashOrderItemDto> items)
        {
            var orderItems = new List<WashOrderItem>();
            decimal total = 0;
            var hasWeightService = false;
            var baseProcessingHours = 0;
            foreach (var item in items)
            {
                var service = await _serviceRepo.FindAsync(item.ServiceId);
                if (service == null || service.IsDeleted || !service.IsActive ||
                    service.ApprovalStatus != ServiceApprovalStatus.Approved)
                    return (null, "Dịch vụ đã chọn không còn được cung cấp.");

                var isWeightBased = service.Unit.Equals("kg", StringComparison.OrdinalIgnoreCase);
                if (isWeightBased && item.Quantity != 1)
                    return (null, "Dịch vụ theo gói kg chỉ chọn một gói cho mỗi loại dịch vụ.");
                if (!isWeightBased && item.WeightKg.HasValue)
                    return (null, "Dịch vụ theo chiếc/đôi không nhận khối lượng kg.");
                if (service.EstimatedDurationHours is < 1 or > 8760)
                    return (null, "Thời gian xử lý dịch vụ chưa được cấu hình hợp lệ.");

                var unitPrice = isWeightBased ? service.UnitPrice : service.PricePerItem ?? service.UnitPrice;
                if (unitPrice <= 0)
                    return (null, "Giá dịch vụ chưa được cấu hình hợp lệ.");
                var subTotal = isWeightBased ? unitPrice : unitPrice * item.Quantity;
                total += subTotal;
                hasWeightService |= isWeightBased;
                baseProcessingHours = Math.Max(baseProcessingHours, service.EstimatedDurationHours);
                orderItems.Add(new WashOrderItem
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
            return (new PricedOrder(orderItems, total, hasWeightService, baseProcessingHours), null);
        }

        private Task<int> CountCustomerPromotionUsesAsync(Guid promotionId, Guid customerId) =>
            _promotionUsageRepo.CountAsync(x => x.PromotionId == promotionId && x.CustomerId == customerId && !x.IsReverted);

        private async Task<(Promotion? Promotion, decimal Discount, string? Error)> ResolvePromotionAsync(
            string? promotionCode, Guid customerId, decimal orderAmount, DateTime now)
        {
            var code = Promotion.NormalizeCode(promotionCode);
            if (code.Length == 0) return (null, 0m, null);
            var promotion = await _promotionRepo.FirstOrDefaultAsync(x => x.Code == code && !x.IsDeleted);
            if (promotion == null) return (null, 0m, "Mã giảm giá không tồn tại.");
            var used = await CountCustomerPromotionUsesAsync(promotion.Id, customerId);
            var reason = promotion.GetIneligibleReason(now, orderAmount, used);
            return reason != null ? (null, 0m, reason) : (promotion, promotion.CalculateDiscount(orderAmount), null);
        }

        private static AvailablePromotionDto ToAvailableDto(Promotion promotion, DateTime now, decimal orderAmount, int customerUsed)
        {
            var reason = promotion.GetIneligibleReason(now, orderAmount, customerUsed);
            return new AvailablePromotionDto
            {
                Id = promotion.Id,
                Code = promotion.Code,
                Description = promotion.Description,
                DiscountType = promotion.DiscountType,
                DiscountValue = promotion.DiscountValue,
                MinOrderAmount = promotion.MinOrderAmount,
                MaxDiscountAmount = promotion.MaxDiscountAmount,
                EndAt = promotion.EndAt,
                RemainingUsesForCustomer = promotion.UsageLimitPerCustomer.HasValue
                    ? Math.Max(0, promotion.UsageLimitPerCustomer.Value - customerUsed) : null,
                IsEligible = reason == null,
                IneligibleReason = reason,
                EstimatedDiscountAmount = reason == null ? promotion.CalculateDiscount(orderAmount) : 0m
            };
        }

        public async Task<BaseResponse<VoucherQuoteDto>> ApplyVoucherAsync(ApplyVoucherCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return BadRequest<VoucherQuoteDto>("Không xác định được tài khoản khách hàng.");
            var itemError = ValidateItems(command.Items);
            if (itemError != null)
                return BadRequest<VoucherQuoteDto>(itemError);

            var locker = await _lockerRepo.FindAsync(command.LockerId);
            if (locker == null || locker.IsDeleted || locker.Status != LockerStatus.Active)
                return BadRequest<VoucherQuoteDto>("Tủ Locker đã chọn không hoạt động.");
            if (!await _lockerMerchants.HasActiveMerchantAsync(locker.Id))
                return BadRequest<VoucherQuoteDto>("Đối tác quản lý tủ hiện không hoạt động.");

            var (priced, priceError) = await PriceItemsAsync(command.Items);
            if (priced == null)
                return BadRequest<VoucherQuoteDto>(priceError!);

            var customerId = CurrentUser.Id.Value;
            var now = DateTime.UtcNow;
            var promotions = await AsyncExecuter.ToListAsync((await _promotionRepo.GetQueryableAsync())
                .Where(x => !x.IsDeleted && x.IsActive && x.StartAt <= now && x.EndAt >= now
                    && (x.UsageLimit == null || x.UsedCount < x.UsageLimit))
                .OrderBy(x => x.EndAt));
            var promotionIds = promotions.Select(x => x.Id).ToList();
            var usedByPromotion = (await AsyncExecuter.ToListAsync((await _promotionUsageRepo.GetQueryableAsync())
                    .Where(x => x.CustomerId == customerId && !x.IsReverted && promotionIds.Contains(x.PromotionId))
                    .GroupBy(x => x.PromotionId)
                    .Select(g => new { PromotionId = g.Key, Count = g.Count() })))
                .ToDictionary(x => x.PromotionId, x => x.Count);

            var quote = new VoucherQuoteDto
            {
                TotalAmount = priced.Total,
                FinalAmount = priced.Total,
                AvailablePromotions = promotions
                    .Select(p => ToAvailableDto(p, now, priced.Total, usedByPromotion.GetValueOrDefault(p.Id)))
                    .Where(p => p.RemainingUsesForCustomer != 0)
                    .OrderByDescending(p => p.IsEligible).ThenByDescending(p => p.EstimatedDiscountAmount)
                    .ToList()
            };

            if (!string.IsNullOrWhiteSpace(command.PromotionCode))
            {
                var (promotion, discount, error) = await ResolvePromotionAsync(command.PromotionCode, customerId, priced.Total, now);
                if (promotion == null)
                    return BadRequest<VoucherQuoteDto>(error!);
                quote.AppliedPromotion = ToAvailableDto(promotion, now, priced.Total,
                    usedByPromotion.GetValueOrDefault(promotion.Id));
                quote.DiscountAmount = discount;
                quote.FinalAmount = priced.Total - discount;
                return Success(quote, "Áp dụng mã giảm giá thành công.");
            }

            return Success(quote, quote.AvailablePromotions.Any(x => x.IsEligible)
                ? null : "Hiện tại không có mã giảm giá khả dụng cho đơn hàng này.");
        }

        public async Task<BaseResponse<WashOrderDetailDto>> CreateAsync(CreateWashOrderCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (!CurrentUser.Id.HasValue)
            {
                return BadRequest<WashOrderDetailDto>("Không xác định được tài khoản khách hàng.");
            }

            var itemError = ValidateItems(command.Items);
            if (itemError != null)
                return BadRequest<WashOrderDetailDto>(itemError);

            if (!Enum.IsDefined(command.PaymentMethod))
                return BadRequest<WashOrderDetailDto>("Phương thức thanh toán không hợp lệ.");

            var customerId = CurrentUser.Id.Value;
            await _serviceRepo.AcquireLockAsync("service-catalog");
            // Match cancellation's lock order (locker then promotion), avoiding a promo/locker cycle.
            await _lockerRepo.LockAsync(command.LockerId);
            var locker = await _lockerRepo.GetWithBoxesAsync(command.LockerId);
            if (locker == null || locker.Status != LockerStatus.Active)
            {
                return BadRequest<WashOrderDetailDto>("Tủ Locker đã chọn không hoạt động.");
            }

            var lockerMerchants = await _lockerMerchants.ActiveLinksAsync(locker.Id);
            if (lockerMerchants.Count == 0)
                return BadRequest<WashOrderDetailDto>("Đối tác quản lý tủ hiện không hoạt động.");

            if (command.ReturnLockerId.HasValue && command.ReturnLockerId.Value != locker.Id)
            {
                // The Merchant assigned later must serve both lockers.
                var returnLocker = await _lockerRepo.FindAsync(command.ReturnLockerId.Value);
                var returnMerchants = returnLocker == null || returnLocker.Status != LockerStatus.Active
                    ? [] : (await _lockerMerchants.ActiveLinksAsync(returnLocker.Id)).Select(x => x.MerchantId).ToList();
                if (!lockerMerchants.Any(x => returnMerchants.Contains(x.MerchantId)))
                    return BadRequest<WashOrderDetailDto>("Tủ trả đồ phải đang hoạt động và có chung Merchant phụ trách với tủ gửi.");
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

            var (priced, priceError) = await PriceItemsAsync(command.Items);
            if (priced == null)
                return BadRequest<WashOrderDetailDto>(priceError!);
            if (!Enum.IsDefined(box.Size))
                return BadRequest<WashOrderDetailDto>("Kích thước ô tủ chưa được cấu hình hợp lệ.");

            var now = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(command.PromotionCode))
            {
                var promoCode = PromotionEntity.NormalizeCode(command.PromotionCode);
                var promoId = await AsyncExecuter.FirstOrDefaultAsync((await _promotionRepo.GetQueryableAsync())
                    .Where(x => x.Code == promoCode && !x.IsDeleted).Select(x => (Guid?)x.Id));
                if (promoId.HasValue) await _promotionRepo.GetForUpdateAsync(promoId.Value);
            }
            var (promotion, discount, promotionError) = await ResolvePromotionAsync(
                command.PromotionCode, customerId, priced.Total, now);
            if (promotionError != null)
                return BadRequest<WashOrderDetailDto>(promotionError);

            // Locker credentials use cryptographically secure random numbers.
            var depositPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var pickupPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            // Only 9000 codes per day: regenerate on collision instead of failing on the unique index.
            string orderCode;
            var attempts = 0;
            do
            {
                orderCode = $"WG{DateTime.UtcNow:yyMMdd}{RandomNumberGenerator.GetInt32(1000, 10000)}";
            } while (++attempts < 10 && await _orderRepo.AnyAsync(x => x.OrderCode == orderCode));

            var operationalSettings = await _settings.GetAsync();
            var order = new WashOrder
            {
                OrderCode = orderCode,
                // Unique because the order code is; short code for the Merchant to tag the laundry bag (UC 2.3.1).
                MerchantOrderCode = "MC" + orderCode[2..],
                CustomerId = customerId,
                CustomerName = customerName,
                CustomerPhoneNumber = customerPhone,
                // No Merchant yet: the Admin assigns one after the customer deposits (UC 3.3).
                MerchantId = null,
                MerchantName = null,
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
                PromotionId = promotion?.Id,
                PromotionCode = promotion?.Code,
                TotalAmount = priced.Total,
                DiscountAmount = discount,
                FinalAmount = priced.Total - discount,
                IncludedWeightKg = priced.HasWeightService ? WashOrderWeightPricing.IncludedWeight(box.Size) : 0m,
                BaseProcessingHours = priced.BaseProcessingHours,
                ExpectedDeliveryTime = DateTimeOffset.UtcNow.AddHours(priced.BaseProcessingHours).ToUnixTimeSeconds(),
                // The box is held until this time; fixed now so a later SLA change does not move it (UC 3.9.1).
                DepositDeadline = OrderDepositRules.DeadlineFor(DateTime.UtcNow, operationalSettings.DepositWindowMinutes)
            };
            OrderOperationalPolicy.Capture(order, operationalSettings);
            foreach (var item in priced.Items)
                order.Items.Add(item);

            order.Timeline.Add(new WashOrderTimeline
            {
                Status = WashOrderStatus.Pending,
                ActorId = customerId,
                ActorName = customerName,
                ActorRole = "Customer",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = promotion == null
                    ? "Khách hàng khởi tạo đơn qua App/Web"
                    : $"Khách hàng khởi tạo đơn qua App/Web; áp dụng mã {promotion.Code} giảm {discount:N0}đ"
            });

            if (!await _lockerRepo.TryReserveBoxAsync(locker.Id, box.Id))
                return BadRequest<WashOrderDetailDto>("Ô này vừa được đặt. Vui lòng chọn ô khác.");
            if (promotion != null)
            {
                // The consume UPDATE locks the promotion row, so the per-customer recount below is serialized.
                if (!await _promotionRepo.TryConsumeAsync(promotion.Id, now))
                {
                    await uow.RollbackAsync();
                    return BadRequest<WashOrderDetailDto>("Mã giảm giá vừa hết lượt hoặc không còn hiệu lực; vui lòng chọn mã khác.");
                }
                if (promotion.UsageLimitPerCustomer.HasValue &&
                    await CountCustomerPromotionUsesAsync(promotion.Id, customerId) >= promotion.UsageLimitPerCustomer.Value)
                {
                    await uow.RollbackAsync();
                    return BadRequest<WashOrderDetailDto>("Bạn đã dùng hết số lượt cho phép của mã này.");
                }
            }
            await _orderRepo.InsertAsync(order, autoSave: true);
            if (promotion != null)
            {
                await _promotionUsageRepo.InsertAsync(new PromotionUsage
                {
                    PromotionId = promotion.Id,
                    OrderId = order.Id,
                    CustomerId = customerId,
                    OrderAmount = priced.Total,
                    DiscountAmount = discount,
                    UsedAt = now
                }, autoSave: true);
            }
            await _notifications.OrderCreatedAsync(order);
            await uow.CompleteAsync();

            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Created(result, "Tạo đơn hàng WashGo thành công!");
        }

        private async Task<decimal> GetVerifiedPaidAmountAsync(WashOrder order)
        {
            if (order.PaymentStatus != PaymentStatus.Paid) return 0m;
            var payments = await _payments.GetListAsync(x => x.OrderId == order.Id &&
                x.Purpose == PaymentPurpose.Order && x.Status == PaymentTransactionStatus.Succeeded);
            return payments.Count > 0 ? payments.Sum(x => x.Amount)
                : order.PaymentMethod == PaymentMethod.Cash ? order.FinalAmount : 0m;
        }

        public async Task<BaseResponse<CancellationQuoteDto>> GetCancellationQuoteAsync(Guid orderId)
        {
            var scope = await GetOrderScopeAsync();
            var order = await _orderRepo.FindAsync(orderId);
            if (scope.Role != "Customer" || order == null || order.CustomerId != scope.Id)
                return NotFound<CancellationQuoteDto>("Không tìm thấy đơn hàng của khách hàng.");
            var allowed = OrderCancellationPolicy.TryCalculate(order, DateTime.UtcNow, await _settings.ForOrderAsync(order),
                out var basis, out var percent, out var fee);
            var paid = await GetVerifiedPaidAmountAsync(order);
            allowed = allowed && (order.PaymentStatus != PaymentStatus.Paid || paid > 0m);
            if (order.Status == WashOrderStatus.Cancelled)
            {
                basis = order.CancellationFromStatus == WashOrderStatus.Collected ? order.FinalAmount : order.TotalAmount;
                // Show the percentage actually charged, even if the configured rates changed later.
                percent = basis > 0m ? (int)decimal.Round(order.CancelFeeAmount * 100m / basis, 0, MidpointRounding.AwayFromZero) : 0;
            }
            return Success(new CancellationQuoteDto
            {
                OrderId = order.Id, Status = order.Status, CanCancel = allowed,
                Reason = allowed ? null : order.Status == WashOrderStatus.Cancelled ? "Đơn đã được hủy; hiển thị phí đã chốt." : "Đơn không đủ điều kiện hủy hoặc không có thanh toán gốc hợp lệ để đối chiếu.",
                BasisAmount = basis, FeePercent = percent,
                FeeAmount = order.Status == WashOrderStatus.Cancelled ? order.CancelFeeAmount : fee,
                RefundAmount = order.Status == WashOrderStatus.Cancelled ? order.CancellationRefundAmount : Math.Max(0m, paid - fee),
                FeeOutstanding = order.Status == WashOrderStatus.Cancelled
                    ? Math.Max(0m, order.CancelFeeAmount - order.CancelFeePaidAmount) : Math.Max(0m, fee - paid)
            }, "Báo phí chỉ là ước tính tại thời điểm xem; hệ thống tính lại khi xác nhận hủy.");
        }

        public async Task<BaseResponse<AdminCancellationQuoteDto>> GetAdminCancellationQuoteAsync(Guid orderId)
        {
            var scope = await GetOrderScopeAsync();
            var order = await _orderRepo.FindAsync(orderId);
            if (scope.Role != "Admin" || order == null || order.IsDeleted)
                return NotFound<AdminCancellationQuoteDto>("Không tìm thấy đơn hàng!");
            return Success(await AdminQuoteAsync(order, scope, DateTime.UtcNow),
                "Báo phí chỉ là ước tính tại thời điểm xem; hệ thống tính lại và đối chiếu khi xác nhận hủy.");
        }

        private async Task<AdminCancellationQuoteDto> AdminQuoteAsync(WashOrder order, OrderScope scope, DateTime now)
        {
            var settings = await _settings.ForOrderAsync(order);
            var block = OrderActionRules.CancelExpiredDeposit(order, scope.Role, now);
            var allowed = OrderCancellationPolicy.TryCalculate(order, now, settings, out var basis, out var percent, out var fee);
            var paid = await GetVerifiedPaidAmountAsync(order);
            if (block == null && !allowed)
                block = new OrderActionRules.Block("NotPending", "Đơn không đủ điều kiện hủy.");
            if (block == null && order.PaymentStatus == PaymentStatus.Paid && paid <= 0m)
                block = new OrderActionRules.Block("NoVerifiedPayment", "Không có giao dịch thanh toán gốc hợp lệ để đối chiếu hoàn tiền.");
            if (block == null && await _refunds.AnyAsync(x => x.OrderId == order.Id && x.Status != RefundRequestStatus.Rejected))
                block = new OrderActionRules.Block("RefundInProgress", "Đơn đã có yêu cầu hoàn tiền; vui lòng xử lý yêu cầu đó trước.");
            return new AdminCancellationQuoteDto
            {
                OrderId = order.Id, Status = order.Status, Revision = order.Revision,
                CanCancel = block == null, ReasonCode = block?.Code, Reason = block?.Message,
                DepositDeadline = OrderDepositRules.DeadlineOf(order, settings.DepositWindowMinutes), ServerNow = now,
                BasisAmount = basis, FeePercent = percent, FeeAmount = fee, PaidAmount = paid,
                RefundAmount = Math.Max(0m, paid - fee), FeeOutstanding = Math.Max(0m, fee - paid)
            };
        }

        public async Task<BaseResponse<WashOrderDetailDto>> CancelExpiredDepositAsync(Guid orderId, AdminCancelExpiredDepositCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role != "Admin")
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng!");
            var reason = command.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                return BadRequest<WashOrderDetailDto>("Lý do hủy phải có từ 1 đến 500 ký tự.");

            // Same row lock as the deposit flow: either the customer deposits or the Admin cancels, never both.
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || order.IsDeleted)
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng!");
            var request = OrderWriteGuard.Fingerprint(
                $"cancelExpiredDeposit;fee={command.ExpectedFeeAmount?.ToString(CultureInfo.InvariantCulture) ?? "-"}", reason);
            if (await GuardAsync(order, scope, command, "CancelExpiredDeposit", request) is { } stop)
                return stop;
            var now = DateTime.UtcNow;
            var quote = await AdminQuoteAsync(order, scope, now);
            if (!quote.CanCancel)
                return new BaseResponse<WashOrderDetailDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = quote.Reason,
                    Meta = new { code = quote.ReasonCode }
                };
            if (command.ExpectedFeeAmount.HasValue && command.ExpectedFeeAmount.Value != quote.FeeAmount)
                return new BaseResponse<WashOrderDetailDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.Conflict, Data = MapForAdmin(order, scope),
                    Message = $"Phí hủy đã thay đổi ({command.ExpectedFeeAmount:N0}đ → {quote.FeeAmount:N0}đ); vui lòng xem lại trước khi xác nhận.",
                    Meta = new { code = "QuoteChanged", feeAmount = quote.FeeAmount }
                };
            // Release only the box still reserved for this pending order; anything else is a conflict and rolls back.
            if (!await _lockerRepo.TryReleaseReservedBoxAsync(order.LockerId, order.BoxId))
            {
                await uow.RollbackAsync();
                return new BaseResponse<WashOrderDetailDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.Conflict,
                    Message = "Ô tủ không còn được giữ chỗ cho đơn này; chưa hủy đơn.", Meta = new { code = "BoxNotReserved" }
                };
            }

            order.CancelledGoodsStatus = CancelledGoodsStatus.None;
            order.Status = WashOrderStatus.Cancelled;
            order.CancellationFromStatus = WashOrderStatus.Pending;
            order.CancelledAt = now;
            order.CancelledBy = CurrentUser.Id;
            order.CancelledByType = OrderActorType.Admin;
            order.CancellationReason = reason;
            order.CancelFeeAmount = quote.FeeAmount;
            order.CancelFeePaidAmount = Math.Min(quote.FeeAmount, quote.PaidAmount);
            order.CancellationRefundAmount = quote.RefundAmount;
            if (order.CancellationRefundAmount > 0m)
            {
                await _refunds.InsertAsync(new RefundRequest
                {
                    OrderId = order.Id, CustomerId = order.CustomerId, Amount = order.CancellationRefundAmount,
                    Reason = $"Hoàn tiền sau khi Admin hủy đơn quá hạn gửi đồ; phí {quote.FeePercent}%. {reason}",
                    Status = RefundRequestStatus.Pending
                }, autoSave: true);
                await _notifications.RefundRequestedAsync(order, order.CancellationRefundAmount);
            }
            var usage = order.PromotionId.HasValue
                ? await _promotionUsageRepo.FirstOrDefaultAsync(x => x.OrderId == order.Id && !x.IsReverted)
                : null;
            if (usage != null)
            {
                usage.IsReverted = true;
                usage.RevertedAt = now;
                await _promotionUsageRepo.UpdateAsync(usage, autoSave: true);
                await _promotionRepo.TryReleaseAsync(usage.PromotionId);
            }
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id, FromStatus = WashOrderStatus.Pending, Status = WashOrderStatus.Cancelled,
                ActorId = CurrentUser.Id, ActorName = CurrentUser.UserName ?? "Admin", ActorRole = "Admin",
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds(),
                Note = $"Admin hủy do quá hạn gửi đồ; phí {quote.FeePercent}% = {quote.FeeAmount:N0}đ; lý do: {reason}."
            });
            await _orderRepo.UpdateAsync(order, autoSave: true);
            await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, "CancelExpiredDeposit", request);
            await _notifications.CancelledAsync(order, null);
            await uow.CompleteAsync();
            return Success(MapForAdmin(order, scope), "Đã hủy đơn quá hạn gửi đồ và giải phóng ô tủ.");
        }

        private WashOrderDetailDto MapForAdmin(WashOrder order, OrderScope scope)
        {
            var dto = RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope);
            dto.AllowedActions = OrderActionRules.Build(order, scope.Role);
            return dto;
        }

        public async Task<BaseResponse<WashOrderDetailDto>> CancelAsync(Guid orderId, CancelWashOrderCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role != "Customer")
                return BadRequest<WashOrderDetailDto>("Chỉ khách hàng được hủy đơn của mình.");
            var reason = command.Reason?.Trim();
            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                return BadRequest<WashOrderDetailDto>("Lý do hủy đơn phải có từ 1 đến 500 ký tự.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order == null || order.CustomerId != scope.Id)
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng của khách hàng.");
            if (order.Status == WashOrderStatus.Cancelled)
                return Success(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), "Đơn hàng đã được hủy trước đó.");
            var now = DateTime.UtcNow;
            if (!OrderCancellationPolicy.TryCalculate(order, now, await _settings.ForOrderAsync(order), out _, out var percent, out var fee))
                return BadRequest<WashOrderDetailDto>("Chỉ được hủy Pending, Deposited hoặc Collected đã cân; không hủy khi đã bắt đầu giặt.");
            var paid = await GetVerifiedPaidAmountAsync(order);
            if (order.PaymentStatus == PaymentStatus.Paid && paid <= 0m)
                return BadRequest<WashOrderDetailDto>("Không có giao dịch thanh toán gốc hợp lệ để đối chiếu hoàn tiền.");
            if (await _refunds.AnyAsync(x => x.OrderId == order.Id && x.Status != RefundRequestStatus.Rejected))
                return BadRequest<WashOrderDetailDto>("Đơn đã có yêu cầu hoàn tiền; vui lòng xử lý yêu cầu đó trước khi hủy.");

            var previous = order.Status;
            var voidedTaskShipperId = previous == WashOrderStatus.Deposited && order.AssignedTaskType == DeliveryTaskType.Collect
                ? order.ShipperId : null;
            if (previous == WashOrderStatus.Pending &&
                !await _lockerRepo.TryReleaseReservedBoxAsync(order.LockerId, order.BoxId))
            {
                await uow.RollbackAsync();
                return BadRequest<WashOrderDetailDto>("Ô tủ không còn được giữ chỗ; chưa hủy đơn.");
            }
            // Occupied slots keep the customer's belongings until authenticated pickup.
            order.CancelledGoodsStatus = previous switch
            {
                WashOrderStatus.Deposited => CancelledGoodsStatus.AtDepositLocker,
                WashOrderStatus.Collected => CancelledGoodsStatus.AtMerchant,
                _ => CancelledGoodsStatus.None
            };
            if (previous == WashOrderStatus.Deposited)
            {
                order.ReturnLockerId = order.LockerId;
                order.ReturnBoxId = order.BoxId;
                order.ReturnBoxNumber = order.BoxNumber;
            }
            order.Status = WashOrderStatus.Cancelled;
            order.CancellationFromStatus = previous;
            order.CancelledAt = now;
            order.CancelledBy = scope.Id;
            order.CancelledByType = OrderActorType.Customer;
            order.CancellationReason = reason;
            order.CancelFeeAmount = fee;
            order.CancelFeePaidAmount = Math.Min(fee, paid);
            order.CancellationRefundAmount = Math.Max(0m, paid - fee);
            order.AssignedTaskType = null;
            order.ShipperTaskPinCode = null;
            order.ShipperTaskQrString = null;
            order.TaskPinAttemptCount = 0;
            order.TaskPinLockoutUntil = null;
            order.PickupSessionTokenHash = null;
            order.PickupSessionExpiresAt = null;
            if (order.CancellationRefundAmount > 0m)
            {
                await _refunds.InsertAsync(new RefundRequest
                {
                    OrderId = order.Id, CustomerId = order.CustomerId,
                    Amount = order.CancellationRefundAmount,
                    Reason = $"Hoàn tiền sau hủy đơn; phí {percent}%. {reason}",
                    Status = RefundRequestStatus.Pending
                }, autoSave: true);
                await _notifications.RefundRequestedAsync(order, order.CancellationRefundAmount);
            }
            // A cancelled order gives the voucher use back to the customer and the global quota.
            var usage = order.PromotionId.HasValue
                ? await _promotionUsageRepo.FirstOrDefaultAsync(x => x.OrderId == order.Id && !x.IsReverted)
                : null;
            if (usage != null)
            {
                usage.IsReverted = true;
                usage.RevertedAt = now;
                await _promotionUsageRepo.UpdateAsync(usage, autoSave: true);
                await _promotionRepo.TryReleaseAsync(usage.PromotionId);
            }
            order.Timeline.Add(new WashOrderTimeline
            {
                WashOrderId = order.Id, FromStatus = previous, Status = WashOrderStatus.Cancelled,
                ActorId = scope.Id, ActorName = CurrentUser.UserName ?? order.CustomerName, ActorRole = "Customer",
                Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds(),
                Note = $"Khách hủy đơn; phí {percent}% = {fee:N0}đ; lý do: {reason}. Vị trí đồ: {order.CancelledGoodsStatus}."
            });
            await _orderRepo.UpdateAsync(order, autoSave: true);
            if (voidedTaskShipperId.HasValue)
                await _taskRecorder.CancelledAsync(order.Id, now);
            await _notifications.CancelledAsync(order, voidedTaskShipperId);
            await uow.CompleteAsync();
            return Success(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order),
                "Đã hủy đơn. Phí còn thiếu phải thanh toán trước khi nhận lại đồ; đồ trong locker chưa được giải phóng.");
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
            var weightRequest = FormattableString.Invariant($"weightKg={command.WeightKg:0.##}");
            if (await GuardAsync(order, scope, command, "UpdateWeight", weightRequest) is { } stop)
                return stop;
            if (OrderActionRules.Weigh(order, scope.Role) is { } blocked)
                return BadRequest<WashOrderDetailDto>(blocked.Message);
            if (order.ActualWeightKg == command.WeightKg)
            {
                await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, "UpdateWeight", weightRequest);
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
            await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, "UpdateWeight", weightRequest);
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
            var statusRequest = OrderWriteGuard.Fingerprint($"status={(int)command.NewStatus}", command.Note);
            if (await GuardAsync(order, scope, command, "UpdateStatus", statusRequest) is { } stop)
                return stop;

            var previousStatus = order.Status;
            if (OrderActionRules.StatusChange(order, scope.Role, command.NewStatus) is { } blocked)
                return BadRequest<WashOrderDetailDto>(blocked.Message);

            if (!await _orderRepo.TryChangeStatusAsync(order.Id, previousStatus, command.NewStatus))
                return BadRequest<WashOrderDetailDto>("Đơn hàng vừa đổi trạng thái; vui lòng tải lại trước khi cập nhật.");

            order.Status = command.NewStatus;
            if (command.NewStatus == WashOrderStatus.AwaitingPayment)
                order.PaymentDeadline = UnpaidOrderStorageMover.DeadlineFrom(DateTime.UtcNow); // UC 1.2.4 7b
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
            await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, "UpdateStatus", statusRequest);
            await _notifications.ProcessingAsync(order);
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
            var storageRequest = OrderWriteGuard.Fingerprint("moveToStorage",
                $"{command.StorageLocation.Trim()}|{command.StorageReference.Trim()}|{command.Note?.Trim()}");
            if (await GuardAsync(order, scope, command, "MoveToStorage", storageRequest) is { } stop)
                return stop;

            var now = DateTime.UtcNow;
            if (OrderActionRules.MoveToStorage(order, scope.Role, now) is { } blocked)
                return BadRequest<WashOrderDetailDto>(blocked.Message);

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
            order.LockerOverdueFeeAtStorageTransfer = OverdueFeeCalculator.Calculate(order.PickupDeadline, now, await _settings.ForOrderAsync(order));
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
            await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, "MoveToStorage", storageRequest);
            await _notifications.MovedToStorageAsync(order);
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
            var assignRequest = OrderWriteGuard.Fingerprint($"shipperId={command.ShipperId}",
                string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim());
            if (await GuardAsync(order, scope, command, "AssignShipper", assignRequest) is { } stop)
                return stop;

            if (OrderActionRules.Assign(order, scope.Role, out var task) is { } blocked)
                return BadRequest<WashOrderDetailDto>(blocked.Message);
            var note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
            if (note?.Length > 500)
                return BadRequest<WashOrderDetailDto>("Ghi chú cho Shipper tối đa 500 ký tự.");

            // Serialize assignments for the same Shipper, including different orders.
            var shipper = await _shipperRepo.GetForUpdateAsync(command.ShipperId);
            if (shipper == null || shipper.MerchantId != order.MerchantId)
                return BadRequest<WashOrderDetailDto>("Shipper phải thuộc cùng tiệm với đơn hàng.");
            var user = await _userRepo.FindAsync(shipper.UserId);
            if (!shipper.IsActive || shipper.Status != ShipperStatus.Available || user == null || !user.IsActive ||
                !await _roleRepo.AnyAsync(x => x.Id == user.RoleId && x.Name == "Shipper" && x.IsActive))
                return BadRequest<WashOrderDetailDto>("Shipper không hoạt động hoặc không sẵn sàng nhận chuyến.");
            if (await AsyncExecuter.AnyAsync(DeliveryTaskRules.ActiveDeliveries(await _orderRepo.GetQueryableAsync())
                    .Where(x => x.Id != order.Id && x.ShipperId == shipper.Id)))
                return BadRequest<WashOrderDetailDto>("Shipper đang có chuyến chưa hoàn tất; vui lòng chọn người khác.");

            // Generate a secure 6-digit PIN and task QR for this assignment
            var taskPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var taskTypeStr = task == DeliveryTaskType.Collect ? "COLLECT" : "RETURN";
            var taskQr = $"WASHGO:{taskTypeStr}:{order.OrderCode}:{taskPin}";

            if (order.ShipperId == shipper.Id && order.AssignedTaskType == task)
            {
                if (string.IsNullOrEmpty(order.ShipperTaskPinCode) || command.Note != null && order.ShipperNote != note)
                {
                    if (string.IsNullOrEmpty(order.ShipperTaskPinCode))
                    {
                        order.ShipperTaskPinCode = taskPin;
                        order.ShipperTaskQrString = taskQr;
                        order.TaskPinAttemptCount = 0;
                        order.TaskPinLockoutUntil = null;
                    }
                    if (command.Note != null)
                        order.ShipperNote = note;
                    await _orderRepo.UpdateAsync(order, autoSave: true);
                }
                await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, "AssignShipper", assignRequest);
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Shipper đã được phân công cho nhiệm vụ này.");
            }

            order.ShipperId = shipper.Id;
            order.ShipperName = user.FullName;
            order.AssignedTaskType = task;
            order.ShipperAssignedAt = DateTime.UtcNow;
            await _taskRecorder.AssignedAsync(order, shipper.Id, task!.Value, CurrentUser.Id, order.ShipperAssignedAt.Value);
            order.ShipperNote = note;
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
            await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, "AssignShipper", assignRequest);
            await _notifications.ShipperAssignedAsync(order, order.AssignedTaskType ?? DeliveryTaskType.Collect);
            await uow.CompleteAsync();
            var result = ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order);
            return Success(RedactStaffData(result, scope), "Phân công Shipper thành công!");
        }

        // Replay or 409 (with the current order) before any rule runs; null = go on with the write.
        private async Task<BaseResponse<WashOrderDetailDto>?> GuardAsync(WashOrder order, OrderScope scope,
            GuardedOrderCommand command, string operation, string request)
        {
            var decision = await _writeGuard.CheckAsync(order, CurrentUser.Id ?? Guid.Empty, command, operation, request);
            if (decision.Outcome == OrderWriteGuard.Outcome.Proceed)
                return null;
            if (decision.Outcome == OrderWriteGuard.Outcome.Invalid)
                return BadRequest<WashOrderDetailDto>(decision.Message!);
            var dto = RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope);
            dto.AllowedActions = OrderActionRules.Build(order, scope.Role);
            var replay = decision.Outcome == OrderWriteGuard.Outcome.Replay;
            return new BaseResponse<WashOrderDetailDto>
            {
                Succeeded = replay,
                StatusCode = replay ? HttpStatusCode.OK : HttpStatusCode.Conflict,
                Data = dto,
                Message = decision.Message,
                Meta = new OrderWriteMetaDto
                {
                    Code = decision.Code!, Replayed = replay, OperationId = command.OperationId,
                    ResultRevision = decision.Previous?.ResultRevision, CurrentRevision = order.Revision,
                    CurrentStatus = order.Status
                }
            };
        }

        public async Task<BaseResponse<OrderOperationDto>> GetOperationAsync(Guid orderId, Guid operationId)
        {
            var scope = await GetOrderScopeAsync();
            var operation = await _writeGuard.FindAsync(operationId);
            var order = operation == null ? null : await _orderRepo.FindAsync(orderId);
            if (operation == null || order == null || operation.OrderId != orderId || !CanAccess(order, scope) ||
                scope.Role != "Admin" && operation.ActorId != CurrentUser.Id)
                return NotFound<OrderOperationDto>("Không tìm thấy thao tác này: yêu cầu chưa tới server hoặc đã thất bại, có thể gửi lại cùng operationId.");
            return Success(new OrderOperationDto
            {
                OperationId = operation.Id, OrderId = order.Id, Operation = operation.Operation, Request = operation.Request,
                CreatedAt = operation.CreatedAt, ResultStatus = operation.ResultStatus, ResultRevision = operation.ResultRevision,
                CurrentStatus = order.Status, CurrentRevision = order.Revision
            });
        }

        public async Task<BaseResponse<List<AvailableShipperDto>>> GetAvailableShippersAsync(Guid orderId)
        {
            var scope = await GetOrderScopeAsync();
            var order = await _orderRepo.FindAsync(orderId);
            if (order == null || scope.Role is not ("Admin" or "Merchant") || !CanAccess(order, scope))
                return NotFound<List<AvailableShipperDto>>("Không tìm thấy đơn hàng được phép phân công.");
            var task = OrderActionRules.AssignableTask(order);
            if (!task.HasValue)
                return BadRequest<List<AvailableShipperDto>>("Đơn chưa ở giai đoạn phân công giao nhận.");

            var shippers = await _shipperRepo.GetQueryableAsync();
            var users = await _userRepo.GetQueryableAsync();
            var roles = await _roleRepo.GetQueryableAsync();
            var activeOrders = DeliveryTaskRules.ActiveDeliveries(await _orderRepo.GetQueryableAsync());
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
                                 Id = shipper.Id, FullName = user.FullName, AvatarUrl = user.Avatar, Phone = user.Phone, Email = user.Email,
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

            // Reservation window fixed when the order was created (UC 3.9.1).
            if (OrderDepositRules.IsExpired(order, DateTime.UtcNow, (await _settings.ForOrderAsync(order)).DepositWindowMinutes))
            {
                return BadRequest<WashOrderDetailDto>("Đã quá hạn gửi đồ. Vui lòng hủy đơn để giải phóng ô và tạo đơn mới.");
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
            await _notifications.DepositedAsync(order);
            await uow.CompleteAsync();
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Gửi đồ vào tủ thành công! Đơn hàng đã chuyển sang trạng thái đã gửi đồ.");
        }

        [DisableAuditing]
        public async Task<BaseResponse<WashOrderDetailDto>> ShipperCollectAsync(Guid orderId, ShipperCollectOrderCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role is not ("Shipper" or "Admin"))
                return BadRequest<WashOrderDetailDto>("Chỉ Shipper được phân công hoặc Admin mới có quyền lấy đồ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order != null)
            {
                var guard = await _deliveryGuard.CheckAsync(order, CurrentUser.Id ?? Guid.Empty, command, DeliveryTaskType.Collect, scope.Role == "Shipper", CanAccess(order, scope));
                if (guard != null) return guard;
            }
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
            if (isLocked) return ShipperDeliveryGuard.CredentialError(order, true);

            var pinMatches = !string.IsNullOrWhiteSpace(command.PinCode) &&
                string.Equals(command.PinCode.Trim(), order.ShipperTaskPinCode, StringComparison.OrdinalIgnoreCase);
            var qrMatches = !string.IsNullOrWhiteSpace(command.QrCode) &&
                string.Equals(command.QrCode.Trim(), order.ShipperTaskQrString, StringComparison.OrdinalIgnoreCase);

            if (!pinMatches && !qrMatches)
            {
                var currentAttempts = await RecordFailedAttemptAsync(order, PinKind.ShipperTask);
                // Commit the failed attempt while holding the order lock; no locker action has run.
                await uow.CompleteAsync();
                return ShipperDeliveryGuard.CredentialError(order, currentAttempts >= 5);
            }

            if (!await _lockerRepo.TryReleaseOccupiedBoxAsync(order.LockerId, order.BoxId))
            {
                await uow.RollbackAsync();
                return BadRequest<WashOrderDetailDto>("Ô gửi đồ không còn ở trạng thái chứa đồ; chưa đổi trạng thái đơn.");
            }

            // SLA Check: 4 hours from deposited time
            var depositedAt = order.DepositedAt ?? DateTime.UtcNow;
            var elapsed = DateTime.UtcNow - depositedAt;
            var collectSla = (await _settings.ForOrderAsync(order)).MerchantCollectSlaHours;
            var isOverdueSla = elapsed > TimeSpan.FromHours(collectSla);
            var slaWarning = isOverdueSla ? $" (Cảnh báo: Trễ SLA lấy đồ {elapsed.TotalHours:0.#} giờ so với cam kết {collectSla} giờ)" : string.Empty;

            var previousStatus = order.Status;
            order.Status = WashOrderStatus.Collected;
            await CountDeliveryAsync(order.ShipperId);
            order.TaskPinAttemptCount = 0;
            order.TaskPinLockoutUntil = null;

            var completion = new WashOrderTimeline(Guid.NewGuid())
            {
                WashOrderId = order.Id,
                FromStatus = previousStatus,
                Status = WashOrderStatus.Collected,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? (scope.Role == "Admin" ? "Admin" : order.ShipperName ?? "Shipper"),
                ActorRole = scope.Role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Shipper đã mở ô tủ và lấy đồ về tiệm thành công.{slaWarning}"
            };
            order.Timeline.Add(completion);
            await _taskRecorder.CompletedAsync(order, DeliveryTaskType.Collect, completion);

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await _deliveryGuard.RecordAsync(order, CurrentUser.Id ?? Guid.Empty, command, DeliveryTaskType.Collect,
                RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope));
            await _notifications.CollectedAsync(order);
            await uow.CompleteAsync();
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                "Shipper lấy đồ thành công! Đơn hàng đã chuyển sang trạng thái đã thu gom.");
        }

        [DisableAuditing]
        public async Task<BaseResponse<WashOrderDetailDto>> ShipperReturnAsync(Guid orderId, ShipperReturnOrderCommand command)
        {
            var scope = await GetOrderScopeAsync();
            if (scope.Role is not ("Shipper" or "Admin"))
                return BadRequest<WashOrderDetailDto>("Chỉ Shipper được phân công hoặc Admin mới có quyền trả đồ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _orderRepo.GetForUpdateAsync(orderId);
            var order = await _orderRepo.GetWithDetailsAsync(orderId);
            if (order != null)
            {
                var guard = await _deliveryGuard.CheckAsync(order, CurrentUser.Id ?? Guid.Empty, command, DeliveryTaskType.Return, scope.Role == "Shipper", CanAccess(order, scope));
                if (guard != null) return guard;
            }
            if (order == null || !CanAccess(order, scope))
                return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng.");

            if (scope.Role == "Shipper")
            {
                if (order.ShipperId != scope.Id || order.AssignedTaskType != DeliveryTaskType.Return)
                {
                    return BadRequest<WashOrderDetailDto>("Bạn chưa được phân công hoặc không có nhiệm vụ trả đồ cho đơn này.");
                }
            }

            var cancelledReturn = order.Status == WashOrderStatus.Cancelled &&
                order.CancellationFromStatus == WashOrderStatus.Collected;
            if (!cancelledReturn && order.PaymentStatus != PaymentStatus.Paid)
            {
                return BadRequest<WashOrderDetailDto>("Đơn hàng phải được thanh toán (PAID) trước khi Shipper trả đồ vào tủ.");
            }

            // Idempotency: already ready to return
            if (order.Status == WashOrderStatus.ReadyToReturn || cancelledReturn && order.CancelledGoodsStatus is CancelledGoodsStatus.AtReturnLocker or CancelledGoodsStatus.ReturnedToCustomer)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Thao tác trả đồ đã được thực hiện trước đó.");
            }

            if (order.Status != WashOrderStatus.Paid && !(cancelledReturn && order.CancelledGoodsStatus == CancelledGoodsStatus.AtMerchant))
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa ở trạng thái sẵn sàng để trả về tủ (phải là Paid).");

            if (!order.ShipperId.HasValue || order.AssignedTaskType != DeliveryTaskType.Return)
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa được phân công chuyến trả đồ.");

            // Check lockout
            var (isLocked, remainingMinutes) = CheckLockout(order.TaskPinAttemptCount, order.TaskPinLockoutUntil);
            if (isLocked) return ShipperDeliveryGuard.CredentialError(order, true);

            var pinMatches = !string.IsNullOrWhiteSpace(command.PinCode) &&
                string.Equals(command.PinCode.Trim(), order.ShipperTaskPinCode, StringComparison.OrdinalIgnoreCase);
            var qrMatches = !string.IsNullOrWhiteSpace(command.QrCode) &&
                string.Equals(command.QrCode.Trim(), order.ShipperTaskQrString, StringComparison.OrdinalIgnoreCase);

            if (!pinMatches && !qrMatches)
            {
                var currentAttempts = await RecordFailedAttemptAsync(order, PinKind.ShipperTask);
                // Commit the failed attempt while holding the order lock; no locker action has run.
                await uow.CompleteAsync();
                return ShipperDeliveryGuard.CredentialError(order, currentAttempts >= 5);
            }

            var returnLockerId = order.ReturnLockerId ?? order.LockerId;
            var returnLocker = await _lockerRepo.GetWithBoxesAsync(returnLockerId);
            if (returnLocker == null || returnLocker.Status != LockerStatus.Active ||
                !await _lockerMerchants.ServesAsync(returnLocker.Id, order.MerchantId))
            {
                await uow.RollbackAsync();
                return ShipperDeliveryGuard.Error("LockerUnavailable", "Tủ trả đồ không còn hoạt động hoặc không thuộc đối tác của đơn.");
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
                return ShipperDeliveryGuard.Error("NoSuitableBox", "Tủ trả đồ hiện không có ô trống phù hợp kích thước; chưa thể trả đồ.");
            }

            // SLA Check: 24 hours from payment time
            var paidAt = order.PaidAt ?? DateTime.UtcNow;
            var elapsedReturn = DateTime.UtcNow - paidAt;
            var returnSettings = await _settings.ForOrderAsync(order);
            var isOverdueReturn = !cancelledReturn && elapsedReturn > TimeSpan.FromHours(returnSettings.MerchantReturnSlaHours);
            var slaReturnWarning = isOverdueReturn ? $" (Cảnh báo: Trễ SLA trả đồ {elapsedReturn.TotalHours:0.#} giờ so với cam kết {returnSettings.MerchantReturnSlaHours} giờ)" : string.Empty;

            var previousStatus = order.Status;
            order.ReturnLockerId = returnLockerId;
            order.ReturnBoxId = returnBox.Id;
            order.ReturnBoxNumber = returnBox.BoxNumber;
            order.Status = cancelledReturn ? WashOrderStatus.Cancelled : WashOrderStatus.ReadyToReturn;
            if (cancelledReturn) order.CancelledGoodsStatus = CancelledGoodsStatus.AtReturnLocker;
            await CountDeliveryAsync(order.ShipperId);
            order.PickupDeadline = cancelledReturn ? null : DateTime.UtcNow.AddHours(returnSettings.FreePickupHours);
            order.TaskPinAttemptCount = 0;
            order.TaskPinLockoutUntil = null;

            var completion = new WashOrderTimeline(Guid.NewGuid())
            {
                WashOrderId = order.Id,
                FromStatus = previousStatus,
                Status = order.Status,
                ActorId = CurrentUser.Id,
                ActorName = CurrentUser.UserName ?? (scope.Role == "Admin" ? "Admin" : order.ShipperName ?? "Shipper"),
                ActorRole = scope.Role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Note = $"Đã trả đồ vào ô số {returnBox.BoxNumber} tại tủ {returnLocker.Name}. {(cancelledReturn ? "Đơn hủy chờ khách nhận lại đồ." : $"Khách có {returnSettings.FreePickupHours}h để lấy đồ miễn phí.")}{slaReturnWarning}"
            };
            order.Timeline.Add(completion);
            await _taskRecorder.CompletedAsync(order, DeliveryTaskType.Return, completion);

            await _orderRepo.UpdateAsync(order, autoSave: true);
            await _deliveryGuard.RecordAsync(order, CurrentUser.Id ?? Guid.Empty, command, DeliveryTaskType.Return,
                RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope));
            await _notifications.ReturnedToLockerAsync(order);
            await uow.CompleteAsync();
            return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                cancelledReturn ? "Đã trả đồ của đơn hủy vào locker; đơn vẫn Cancelled, chờ khách nhận lại." : "Shipper trả đồ vào tủ thành công! Đơn hàng đã chuyển sang trạng thái chờ khách nhận.");
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

            var cancelledPickup = OrderCancellationPolicy.IsLockerPickup(order);
            if (cancelledPickup && !OrderCancellationPolicy.FeeSettled(order))
                return BadRequest<OpenPickupBoxResultDto>("Cần thanh toán đủ phí hủy trước khi nhận lại đồ.");
            if (order.Status != WashOrderStatus.ReadyToReturn && !cancelledPickup)
                return BadRequest<OpenPickupBoxResultDto>("Đơn hàng không ở trạng thái sẵn sàng nhận đồ tại tủ.");

            if (!OrderCancellationPolicy.IsLockerPickup(order) && order.PaymentStatus != PaymentStatus.Paid)
                return BadRequest<OpenPickupBoxResultDto>("Đơn hàng chưa được thanh toán.");

            if (!cancelledPickup && order.PickupDeadline.HasValue && OverdueFeeCalculator.IsTransferToStorageDue(order.PickupDeadline, DateTime.UtcNow))
            {
                return BadRequest<OpenPickupBoxResultDto>("Đơn hàng đã quá hạn 72 giờ và chuyển sang quy trình lưu kho. Vui lòng liên hệ hỗ trợ hoặc nhận đồ tại kho.");
            }

            var overdueFee = cancelledPickup ? 0m : OverdueFeeCalculator.Calculate(order.PickupDeadline, DateTime.UtcNow, await _settings.ForOrderAsync(order));
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
            if (order.Status is WashOrderStatus.Returned or WashOrderStatus.Completed || order.Status == WashOrderStatus.Cancelled && order.CancelledGoodsStatus == CancelledGoodsStatus.ReturnedToCustomer)
            {
                await uow.CompleteAsync();
                return Success(RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope),
                    "Đơn hàng đã được xác nhận nhận đồ trước đó.");
            }

            if (!OrderCancellationPolicy.IsLockerPickup(order) && order.PaymentStatus != PaymentStatus.Paid)
                return BadRequest<WashOrderDetailDto>("Đơn hàng chưa được thanh toán tiền giặt.");

            var cancelledPickup = OrderCancellationPolicy.IsLockerPickup(order);
            if (cancelledPickup && !OrderCancellationPolicy.FeeSettled(order))
                return BadRequest<WashOrderDetailDto>("Cần thanh toán đủ phí hủy trước khi nhận lại đồ.");
            if (order.Status != WashOrderStatus.ReadyToReturn && !cancelledPickup)
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

            var overdueFee = cancelledPickup ? 0m : OverdueFeeCalculator.Calculate(order.PickupDeadline, DateTime.UtcNow, await _settings.ForOrderAsync(order));
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

            if (cancelledPickup)
            {
                order.CancelledGoodsStatus = CancelledGoodsStatus.ReturnedToCustomer;
                order.CancelledGoodsReturnedAt = DateTime.UtcNow;
                order.Timeline.Add(new WashOrderTimeline
                {
                    WashOrderId = order.Id, FromStatus = order.Status, Status = order.Status,
                    ActorId = CurrentUser.Id, ActorName = CurrentUser.UserName ?? order.CustomerName,
                    ActorRole = "Customer", Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Note = "Khách đã nhận lại đồ của đơn hủy và đóng cửa; giải phóng ô. Đơn vẫn là Cancelled."
                });
                await _orderRepo.UpdateAsync(order, autoSave: true);
                await _notifications.CancelledGoodsReturnedAsync(order);
                await uow.CompleteAsync();
                return Success(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), "Đã nhận lại đồ của đơn hủy.");
            }
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
            await _notifications.PickedUpAsync(order);
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
            var totalDue = OverdueFeeCalculator.GetTotalStorageDue(order, now, await _settings.ForOrderAsync(order));
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
            await _notifications.PickedUpAsync(order);
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

        private async Task CountDeliveryAsync(Guid? shipperId)
        {
            if (!shipperId.HasValue) return;
            var shipper = await _shipperRepo.FindAsync(shipperId.Value);
            if (shipper == null) return;
            shipper.TotalDeliveries++;
            await _shipperRepo.UpdateAsync(shipper, autoSave: true);
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
            if (query.DateRangeError() is { } dateError)
                return BadRequest<List<ColumnFilterDistinctValueDto>>(dateError);
            var result = await _orderRepo.GetColumnDistinctValuesAsync(fieldName, query,
                baseFilter: q => ApplyScope(q, scope));
            return Success(result);
        }

        private static bool IsStaffField(string field) => new[]
        {
            "OrderCode", "LockerName", "MerchantName", "ShipperName", "Status",
            "PaymentStatus", "PaymentMethod", "CreatedOn", "ExpectedDeliveryTime", "ShipperAssignedAt", "ReturnDeadline"
        }.Contains(field, StringComparer.OrdinalIgnoreCase);

        #endregion
    }
}
