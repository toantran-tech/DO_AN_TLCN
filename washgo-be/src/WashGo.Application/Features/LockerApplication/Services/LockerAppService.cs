using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WashGo.Application.Contracts.Features.LockerContracts.Commands;
using WashGo.Application.Contracts.Features.LockerContracts.Queries;
using WashGo.Application.Contracts.Features.LockerContracts.Results;
using WashGo.Application.Contracts.Features.LockerContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace WashGo.Application.Features.LockerApplication.Services
{
    public class LockerAppService : WashGoAppService, ILockerAppService
    {
        private readonly ILockerRepository _lockerRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;

        public LockerAppService(
            ILockerRepository lockerRepo,
            IWashOrderRepository orderRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo)
        {
            _lockerRepo = lockerRepo;
            _orderRepo = orderRepo;
            _timelineRepo = timelineRepo;
        }

        // UC 2.5: Merchants only see lockers assigned to them (Merchant id = user id); Admin sees all.
        private Guid? ScopeMerchantId => CurrentUser.IsInRole("Admin") ? null : CurrentUser.Id;

        public async Task<PaginatedResult<LockerResultDto>> GetListAsync(LockerFilterQuery query)
        {
            var (data, total) = await _lockerRepo.FilterProjectedDataAsync<LockerResultDto>(
                filterFunc: q =>
                {
                    q = q.Where(x => !x.IsDeleted);
                    if (ScopeMerchantId.HasValue)
                        q = q.Where(x => x.MerchantId == ScopeMerchantId.Value);
                    if (query.Status.HasValue)
                        q = q.Where(x => x.Status == query.Status.Value);
                    if (!string.IsNullOrWhiteSpace(query.City))
                        q = q.Where(x => x.City == query.City);
                    if (!string.IsNullOrWhiteSpace(query.District))
                        q = q.Where(x => x.District == query.District);
                    if (!string.IsNullOrWhiteSpace(query.Keyword))
                    {
                        var kw = query.Keyword.Trim().ToLower();
                        q = q.Where(x => x.Code.ToLower().Contains(kw) ||
                                         x.Name.ToLower().Contains(kw) ||
                                         x.Address.ToLower().Contains(kw));
                    }
                    return q;
                },
                parameters: query,
                defaultOrder: q => q.OrderBy(x => x.Code)
            );

            return Success(data, total, query.Page, query.Take,
                total == 0 && ScopeMerchantId.HasValue ? "Bạn chưa được gán locker nào." : null);
        }

        public async Task<BaseResponse<LockerResultDto>> GetByIdAsync(Guid id)
        {
            var locker = await _lockerRepo.GetWithBoxesAsync(id);
            if (locker == null || (ScopeMerchantId.HasValue && locker.MerchantId != ScopeMerchantId.Value))
            {
                return NotFound<LockerResultDto>("Không tìm thấy thông tin tủ Locker!");
            }

            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            return Success(dto);
        }

        public async Task<BaseResponse<LockerResultDto>> CreateAsync(CreateLockerCommand command)
        {
            var locker = new Locker
            {
                MerchantId = command.MerchantId,
                ServiceAreaId = command.ServiceAreaId,
                Code = command.Code,
                Name = command.Name,
                Address = command.Address,
                Ward = command.Ward,
                District = command.District,
                City = command.City,
                Latitude = command.Latitude,
                Longitude = command.Longitude,
                Status = LockerStatus.Active,
                TotalBoxes = command.Boxes.Count,
                AvailableBoxes = command.Boxes.Count,
                LockerType = command.LockerType,
                Description = command.Description
            };

            foreach (var b in command.Boxes)
            {
                locker.Boxes.Add(new LockerBox
                {
                    BoxNumber = b.BoxNumber,
                    Size = b.Size,
                    Status = LockerBoxStatus.Available
                });
            }

            await _lockerRepo.InsertAsync(locker, autoSave: true);
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            return Created(dto, "Khởi tạo trạm Locker thành công!");
        }

        public async Task<BaseResponse<LockerResultDto>> UpdateAsync(UpdateLockerCommand command)
        {
            if (!Enum.IsDefined(command.Status))
                return BadRequest<LockerResultDto>("Trạng thái tủ không hợp lệ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var locker = await _lockerRepo.GetWithBoxesAsync(command.Id);
            if (locker == null)
            {
                return NotFound<LockerResultDto>("Không tìm thấy trạm Locker để cập nhật!");
            }

            var expectedStatus = locker.Status;
            locker.Name = command.Name;
            locker.Address = command.Address;
            locker.Ward = command.Ward;
            locker.District = command.District;
            locker.City = command.City;
            locker.Latitude = command.Latitude;
            locker.Longitude = command.Longitude;
            locker.LockerType = command.LockerType;
            locker.Description = command.Description;
            locker.Status = command.Status;

            if (!await _lockerRepo.TryUpdateDetailsAsync(locker, expectedStatus))
                return BadRequest<LockerResultDto>(
                    "Tủ đang có ô/đơn cần xử lý hoặc vừa đổi trạng thái; không thể cập nhật trạng thái tủ.");
            await uow.CompleteAsync();
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            return Success(dto, "Cập nhật thông tin trạm Locker thành công!");
        }

        public async Task<BaseResponse<bool>> UpdateBoxStatusAsync(UpdateLockerBoxStatusCommand command)
        {
            if (!Enum.IsDefined(command.NewStatus))
                return BadRequest<bool>("Trạng thái ô tủ không hợp lệ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var locker = await _lockerRepo.GetWithBoxesAsync(command.LockerId);
            if (locker == null)
            {
                return NotFound<bool>("Không tìm thấy tủ Locker!");
            }

            var box = locker.Boxes.FirstOrDefault(b => b.Id == command.BoxId && !b.IsDeleted);
            if (box == null)
            {
                return NotFound<bool>("Không tìm thấy ô tủ!");
            }

            if (box.Status == command.NewStatus)
                return Success(true, "Ô tủ đã ở trạng thái yêu cầu.");
            if (!await _lockerRepo.TrySetUnassignedBoxStatusAsync(locker.Id, box.Id,
                    box.Status, command.NewStatus))
                return BadRequest<bool>("Ô tủ đang gắn với đơn hàng hoặc vừa đổi trạng thái; không thể cập nhật trực tiếp.");
            await uow.CompleteAsync();
            return Success(true, "Cập nhật trạng thái ô tủ thành công!");
        }

        public async Task<PaginatedResult<LockerUsageDto>> GetUsageHistoryAsync(Guid id, LockerUsageQuery query)
        {
            var locker = await _lockerRepo.FindAsync(id);
            if (locker == null || locker.IsDeleted || (ScopeMerchantId.HasValue && locker.MerchantId != ScopeMerchantId.Value))
                return new PaginatedResult<LockerUsageDto>
                {
                    Succeeded = false,
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Message = "Không tìm thấy thông tin tủ Locker!"
                };

            var orders = await AsyncExecuter.ToListAsync((await _orderRepo.GetQueryableAsync())
                .Where(x => !x.IsDeleted && ((x.LockerId == id && x.DepositedAt != null) ||
                                             (x.ReturnLockerId == id && x.ReturnBoxNumber != null))));
            var orderIds = orders.Select(x => x.Id).ToList();
            var timeline = (await AsyncExecuter.ToListAsync((await _timelineRepo.GetQueryableAsync())
                    .Where(x => orderIds.Contains(x.WashOrderId) &&
                                (x.Status == WashOrderStatus.Deposited || x.Status == WashOrderStatus.Collected ||
                                 x.Status == WashOrderStatus.ReadyToReturn ||
                                 x.Status == WashOrderStatus.Returned))))
                .GroupBy(x => (x.WashOrderId, x.Status))
                .ToDictionary(g => g.Key, g => DateTimeOffset.FromUnixTimeSeconds(g.Min(x => x.Timestamp)).UtcDateTime);
            DateTime? At(Guid orderId, WashOrderStatus status) =>
                timeline.TryGetValue((orderId, status), out var at) ? at : null;

            var rows = new List<LockerUsageDto>();
            foreach (var o in orders)
            {
                // Goods cancelled while still in the deposit box never left it until the customer took them back.
                var cancelledInDepositBox = o.Status == WashOrderStatus.Cancelled &&
                                            o.CancellationFromStatus == WashOrderStatus.Deposited;
                if (o.LockerId == id && o.DepositedAt != null)
                    rows.Add(new LockerUsageDto
                    {
                        OrderId = o.Id, OrderCode = o.OrderCode, BoxNumber = o.BoxNumber, UsageType = 1,
                        // Timeline times share one precision, so put-in and taken-out compare correctly.
                        PutInAt = At(o.Id, WashOrderStatus.Deposited) ?? o.DepositedAt,
                        TakenOutAt = cancelledInDepositBox ? o.CancelledGoodsReturnedAt : At(o.Id, WashOrderStatus.Collected),
                        OrderStatus = o.Status
                    });
                if (o.ReturnLockerId == id && o.ReturnBoxNumber != null && !cancelledInDepositBox)
                {
                    var putIn = At(o.Id, WashOrderStatus.ReadyToReturn);
                    var cancelledReturn = o.Status == WashOrderStatus.Cancelled;
                    if (putIn == null && !cancelledReturn)
                        continue;
                    rows.Add(new LockerUsageDto
                    {
                        OrderId = o.Id, OrderCode = o.OrderCode, BoxNumber = o.ReturnBoxNumber, UsageType = 2,
                        PutInAt = putIn,
                        TakenOutAt = cancelledReturn ? o.CancelledGoodsReturnedAt
                            : At(o.Id, WashOrderStatus.Returned) ?? o.MovedToStorageAt,
                        OrderStatus = o.Status
                    });
                }
            }

            IEnumerable<LockerUsageDto> filtered = rows;
            if (query.FromDate is > 0)
            {
                var from = DateTimeOffset.FromUnixTimeSeconds(query.FromDate.Value).UtcDateTime;
                filtered = filtered.Where(x => x.PutInAt >= from);
            }
            if (query.ToDate is > 0)
            {
                var to = DateTimeOffset.FromUnixTimeSeconds(query.ToDate.Value).UtcDateTime;
                filtered = filtered.Where(x => x.PutInAt <= to);
            }
            var list = filtered.OrderByDescending(x => x.PutInAt ?? DateTime.MinValue).ToList();
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            return Success(list.Skip((page - 1) * take).Take(take).ToList(), list.Count, page, take,
                list.Count == 0 ? "Locker này chưa được sử dụng." : null);
        }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, LockerFilterQuery query)
        {
            var merchantId = ScopeMerchantId;
            var result = await _lockerRepo.GetColumnDistinctValuesAsync(fieldName, query,
                merchantId.HasValue ? q => q.Where(x => x.MerchantId == merchantId.Value) : null);
            return Success(result);
        }
    }
}
