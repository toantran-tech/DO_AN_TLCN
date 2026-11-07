using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using WashGo.Application.Contracts.Features.LockerContracts.Commands;
using WashGo.Application.Contracts.Features.LockerContracts.Queries;
using WashGo.Application.Contracts.Features.LockerContracts.Results;
using WashGo.Application.Contracts.Features.LockerContracts.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace WashGo.Application.Features.LockerApplication.Services
{
    public class LockerAppService : WashGoAppService, ILockerAppService
    {
        private const int MaxBoxes = 200;

        private readonly ILockerRepository _lockerRepo;
        private readonly IWashOrderRepository _orderRepo;
        private readonly IRepository<WashOrderTimeline, Guid> _timelineRepo;
        private readonly IRepository<LockerBox, Guid> _boxRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<LockerMerchant, Guid> _linkRepo;
        private readonly IRepository<ServiceArea, Guid> _areaRepo;
        private readonly IConfiguration _configuration;

        public LockerAppService(
            ILockerRepository lockerRepo,
            IWashOrderRepository orderRepo,
            IRepository<WashOrderTimeline, Guid> timelineRepo,
            IRepository<LockerBox, Guid> boxRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<LockerMerchant, Guid> linkRepo,
            IRepository<ServiceArea, Guid> areaRepo,
            IConfiguration configuration)
        {
            _lockerRepo = lockerRepo;
            _orderRepo = orderRepo;
            _timelineRepo = timelineRepo;
            _boxRepo = boxRepo;
            _merchantRepo = merchantRepo;
            _linkRepo = linkRepo;
            _areaRepo = areaRepo;
            _configuration = configuration;
        }

        // UC 2.5: Merchants only see lockers assigned to them (Merchant id = user id); Admin sees all.
        private Guid? ScopeMerchantId => CurrentUser.IsInRole("Admin") ? null : CurrentUser.Id;

        public async Task<PaginatedResult<LockerResultDto>> GetListAsync(LockerFilterQuery query)
        {
            var links = await _linkRepo.GetQueryableAsync();
            var (data, total) = await _lockerRepo.FilterProjectedDataAsync<LockerResultDto>(
                filterFunc: q =>
                {
                    q = q.Where(x => !x.IsDeleted);
                    if (ScopeMerchantId.HasValue)
                        q = q.Where(x => links.Any(l => l.LockerId == x.Id && l.MerchantId == ScopeMerchantId.Value));
                    if (query.Status.HasValue)
                        q = q.Where(x => x.Status == query.Status.Value);
                    if (!string.IsNullOrWhiteSpace(query.City))
                        q = q.Where(x => x.City == query.City);
                    if (!string.IsNullOrWhiteSpace(query.District))
                        q = q.Where(x => x.District == query.District);
                    if (query.ServiceAreaId.HasValue)
                        q = q.Where(x => x.ServiceAreaId == query.ServiceAreaId.Value);
                    if (query.MerchantId.HasValue)
                        q = q.Where(x => links.Any(l => l.LockerId == x.Id && l.MerchantId == query.MerchantId.Value));
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

            await FillNamesAsync(data);
            return Success(data, total, query.Page, query.Take,
                total > 0 ? null : ScopeMerchantId.HasValue ? "Bạn chưa được gán locker nào." : "Chưa có locker nào.");
        }

        public async Task<BaseResponse<LockerResultDto>> GetByIdAsync(Guid id)
        {
            var locker = await _lockerRepo.GetWithBoxesAsync(id);
            if (locker == null || !await InScopeAsync(locker.Id))
            {
                return NotFound<LockerResultDto>("Không tìm thấy thông tin tủ Locker!");
            }

            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            dto.Boxes = dto.Boxes.Where(b => locker.Boxes.Any(x => x.Id == b.Id && !x.IsDeleted))
                .OrderBy(b => int.TryParse(b.BoxNumber, out var n) ? n : int.MaxValue).ToList();
            await FillNamesAsync([dto]);
            dto.ImageUrls = ReadImages(locker.Images);
            return Success(dto);
        }

        // UC 3.4.2
        public async Task<BaseResponse<LockerResultDto>> CreateAsync(CreateLockerCommand command)
        {
            var error = await ValidateAsync(command, null);
            if (error != null)
                return BadRequest<LockerResultDto>(error);
            var imageError = UploadStorage.ValidateOwnedImages(_configuration, command.ImageUrls, CurrentUser.Id!.Value, 0, out var images);
            if (imageError != null)
                return BadRequest<LockerResultDto>(imageError);

            var boxes = command.Boxes.Count > 0
                ? command.Boxes
                : Enumerable.Range(1, command.BoxCount ?? 0)
                    .Select(i => new CreateLockerBoxDto { BoxNumber = i.ToString(), Size = command.DefaultBoxSize }).ToList();
            if (boxes.Count is 0 or > MaxBoxes)
                return BadRequest<LockerResultDto>($"Số ô phải từ 1 đến {MaxBoxes}.");
            if (boxes.Any(b => !int.TryParse(b.BoxNumber, out var n) || n <= 0 || !Enum.IsDefined(b.Size)))
                return BadRequest<LockerResultDto>("Số ô phải là số nguyên dương và kích thước ô hợp lệ.");
            if (boxes.Select(b => int.Parse(b.BoxNumber)).Distinct().Count() != boxes.Count)
                return BadRequest<LockerResultDto>("Số ô bị trùng.");

            var code = string.IsNullOrWhiteSpace(command.Code) ? $"LK_{Guid.NewGuid():N}"[..11].ToUpperInvariant() : command.Code.Trim();
            // The unique index on code also covers deleted lockers.
            if (await _lockerRepo.AnyAsync(x => x.Code == code))
                return BadRequest<LockerResultDto>("Mã locker này đã được sử dụng.");

            var locker = new Locker
            {
                ServiceAreaId = command.ServiceAreaId,
                Code = code,
                Name = command.Name.Trim(),
                Address = command.Address.Trim(),
                Ward = command.Ward,
                District = command.District?.Trim() ?? string.Empty,
                City = command.City?.Trim() ?? string.Empty,
                Latitude = command.Latitude,
                Longitude = command.Longitude,
                Status = LockerStatus.Active,
                TotalBoxes = boxes.Count,
                AvailableBoxes = boxes.Count,
                LockerType = command.LockerType,
                Description = command.Description,
                Images = images.Count == 0 ? null : JsonSerializer.Serialize(images)
            };

            foreach (var b in boxes)
            {
                locker.Boxes.Add(new LockerBox
                {
                    BoxNumber = b.BoxNumber,
                    Size = b.Size,
                    Status = LockerBoxStatus.Available
                });
            }

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            await _lockerRepo.InsertAsync(locker, autoSave: true);
            await ReplaceMerchantsAsync(locker.Id, MerchantIdsOf(command));
            await uow.CompleteAsync();
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            await FillNamesAsync([dto]);
            dto.ImageUrls = images;
            return Created(dto, "Thêm Locker thành công!");
        }

        // UC 3.4.3
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
            var error = await ValidateAsync(command, locker.Id);
            if (error != null)
                return BadRequest<LockerResultDto>(error);
            var images = ReadImages(locker.Images);
            if (command.ImageUrls != null)
            {
                // Images already on the locker stay valid; new ones must be the caller's uploads.
                var added = command.ImageUrls.Where(x => !images.Contains(x?.Trim() ?? string.Empty)).ToList();
                var imageError = UploadStorage.ValidateOwnedImages(_configuration, added, CurrentUser.Id!.Value, 0, out _);
                if (imageError != null)
                    return BadRequest<LockerResultDto>(imageError);
                images = command.ImageUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
                if (images.Count > UploadStorage.MaxImagesPerSubmission)
                    return BadRequest<LockerResultDto>($"Tối đa {UploadStorage.MaxImagesPerSubmission} ảnh.");
            }

            var expectedStatus = locker.Status;
            locker.Name = command.Name.Trim();
            locker.Address = command.Address.Trim();
            locker.Ward = command.Ward;
            locker.District = command.District?.Trim() ?? string.Empty;
            locker.City = command.City?.Trim() ?? string.Empty;
            locker.Latitude = command.Latitude;
            locker.Longitude = command.Longitude;
            locker.LockerType = command.LockerType;
            locker.Description = command.Description;
            locker.ServiceAreaId = command.ServiceAreaId;
            locker.Images = images.Count == 0 ? null : JsonSerializer.Serialize(images);
            locker.Status = command.Status;

            if (!await _lockerRepo.TryUpdateDetailsAsync(locker, expectedStatus))
                return BadRequest<LockerResultDto>(
                    "Tủ đang có ô/đơn cần xử lý hoặc vừa đổi trạng thái; không thể cập nhật trạng thái tủ.");
            if (MerchantIdsOf(command) is { Count: > 0 } newMerchants)
                await ReplaceMerchantsAsync(locker.Id, newMerchants);
            await uow.CompleteAsync();
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            await FillNamesAsync([dto]);
            dto.ImageUrls = images;
            return Success(dto, "Cập nhật Locker thành công!");
        }

        // UC 3.4.4: soft delete the locker and its boxes; blocked while any unfinished order uses it.
        public async Task<BaseResponse<bool>> DeleteAsync(Guid id)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var locker = await _lockerRepo.GetWithBoxesAsync(id);
            if (locker == null)
                return NotFound<bool>("Không tìm thấy thông tin tủ Locker!");
            var inUse = locker.Boxes.Any(b => !b.IsDeleted && b.Status is LockerBoxStatus.Occupied or LockerBoxStatus.Reserved) ||
                        await _orderRepo.AnyAsync(o => !o.IsDeleted && (o.LockerId == id || o.ReturnLockerId == id) &&
                            (o.Status == WashOrderStatus.Cancelled
                                ? o.CancelledGoodsStatus == CancelledGoodsStatus.AtDepositLocker ||
                                  o.CancelledGoodsStatus == CancelledGoodsStatus.AtReturnLocker
                                : o.Status != WashOrderStatus.Completed && o.Status != WashOrderStatus.MovedToStorage));
            if (inUse)
                return BadRequest<bool>("Không thể xóa locker đang có đơn hàng chưa xử lý.");

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var by = CurrentUser.Id?.ToString();
            foreach (var box in locker.Boxes.Where(b => !b.IsDeleted))
            {
                box.IsDeleted = true;
                box.DeletedOn = now;
                box.DeletedBy = by;
            }
            locker.IsDeleted = true;
            locker.DeletedOn = now;
            locker.DeletedBy = by;
            locker.Status = LockerStatus.Inactive;
            locker.AvailableBoxes = 0;
            await _lockerRepo.UpdateAsync(locker, autoSave: true);
            await uow.CompleteAsync();
            return Success(true, "Xóa Locker thành công!");
        }

        // UC 3.4.5: Admin only moves a box between Trống and Bảo trì; boxes holding goods cannot go to maintenance.
        public async Task<BaseResponse<bool>> UpdateBoxStatusAsync(UpdateLockerBoxStatusCommand command)
        {
            if (command.NewStatus is not (LockerBoxStatus.Available or LockerBoxStatus.Maintenance))
                return BadRequest<bool>("Chỉ có thể chuyển ô tủ sang Trống hoặc Bảo trì.");

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
            if (box.Status is LockerBoxStatus.Occupied or LockerBoxStatus.Reserved)
                return BadRequest<bool>("Ô tủ đang có đồ hoặc đang được giữ chỗ, không thể chuyển sang Bảo trì.");
            if (!await _lockerRepo.TrySetUnassignedBoxStatusAsync(locker.Id, box.Id,
                    box.Status, command.NewStatus))
                return BadRequest<bool>("Ô tủ đang gắn với đơn hàng hoặc vừa đổi trạng thái; không thể cập nhật trực tiếp.");
            await uow.CompleteAsync();
            return Success(true, "Cập nhật ô tủ thành công!");
        }

        private async Task<string?> ValidateAsync(CreateLockerCommand command, Guid? currentId)
        {
            var name = command.Name?.Trim() ?? string.Empty;
            if (name.Length is 0 or > 200)
                return "Tên locker phải có từ 1 đến 200 ký tự.";
            if (string.IsNullOrWhiteSpace(command.Address) || command.Address.Trim().Length > 500)
                return "Địa chỉ không được để trống (tối đa 500 ký tự).";
            if (double.IsNaN(command.Latitude) || double.IsNaN(command.Longitude) ||
                command.Latitude is < -90 or > 90 || command.Longitude is < -180 or > 180 ||
                (command.Latitude == 0 && command.Longitude == 0))
                return "Tọa độ không hợp lệ.";
            if (!Enum.IsDefined(command.LockerType))
                return "Loại locker không hợp lệ.";
            // Several Merchants may share a locker; the Admin picks one per order (UC 3.3).
            var merchantIds = MerchantIdsOf(command);
            if (currentId == null || merchantIds.Count > 0 || command.MerchantIds != null)
            {
                if (merchantIds.Count == 0)
                    return "Vui lòng chọn ít nhất một Merchant phụ trách locker.";
                if (merchantIds.Distinct().Count() != merchantIds.Count)
                    return "Mỗi Merchant chỉ được chọn một lần.";
                var active = await AsyncExecuter.CountAsync((await _merchantRepo.GetQueryableAsync())
                    .Where(x => merchantIds.Contains(x.Id) && x.Status == MerchantStatus.Active));
                if (active != merchantIds.Count)
                    return "Merchant phụ trách không tồn tại hoặc không hoạt động.";
            }
            if (command.ServiceAreaId.HasValue && !await _areaRepo.AnyAsync(x => x.Id == command.ServiceAreaId.Value && x.IsActive))
                return "Khu vực không tồn tại hoặc đã ngừng hoạt động.";
            var normalized = name.ToLower();
            if (await _lockerRepo.AnyAsync(x => !x.IsDeleted && x.Id != currentId && x.Name.ToLower() == normalized))
                return "Tên locker này đã được sử dụng.";
            return null;
        }

        private static List<Guid> MerchantIdsOf(CreateLockerCommand command) =>
            command.MerchantIds is { Count: > 0 } ids ? ids
            : command.MerchantId.HasValue ? [command.MerchantId.Value] : [];

        // Merchants only see lockers they are assigned to (UC 2.5); Admin sees all.
        private async Task<bool> InScopeAsync(Guid lockerId) =>
            !ScopeMerchantId.HasValue ||
            await _linkRepo.AnyAsync(x => x.LockerId == lockerId && x.MerchantId == ScopeMerchantId.Value);

        private async System.Threading.Tasks.Task ReplaceMerchantsAsync(Guid lockerId, List<Guid> merchantIds)
        {
            var existing = await _linkRepo.GetListAsync(x => x.LockerId == lockerId);
            await _linkRepo.DeleteManyAsync(existing, autoSave: true);
            await _linkRepo.InsertManyAsync(merchantIds.Select((id, i) => new LockerMerchant(GuidGenerator.Create())
            {
                LockerId = lockerId,
                MerchantId = id,
                Priority = i + 1
            }), autoSave: true);
        }

        private async System.Threading.Tasks.Task FillNamesAsync(List<LockerResultDto> lockers)
        {
            var lockerIdsForLinks = lockers.Select(x => x.Id).ToList();
            var links = await _linkRepo.GetListAsync(x => lockerIdsForLinks.Contains(x.LockerId));
            var merchantIds = links.Select(x => x.MerchantId).Distinct().ToList();
            var areaIds = lockers.Where(x => x.ServiceAreaId.HasValue).Select(x => x.ServiceAreaId!.Value).Distinct().ToList();
            var merchants = (await _merchantRepo.GetListAsync(x => merchantIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.BusinessName);
            var areas = (await _areaRepo.GetListAsync(x => areaIds.Contains(x.Id))).ToDictionary(x => x.Id, x => x.Name);
            var lockerIds = lockers.Select(x => x.Id).ToList();
            // Projected list rows skip the images column: read it for this page only, keeping the stored order.
            var images = (await AsyncExecuter.ToListAsync((await _lockerRepo.GetQueryableAsync())
                    .Where(x => lockerIds.Contains(x.Id)).Select(x => new { x.Id, x.Images })))
                .ToDictionary(x => x.Id, x => ReadImages(x.Images));
            // Projected list rows do not load boxes, so count empty boxes here (UC 3.4.1 "Số ô trống").
            var empty = (await AsyncExecuter.ToListAsync((await _boxRepo.GetQueryableAsync())
                    .Where(b => lockerIds.Contains(b.LockerId) && !b.IsDeleted && b.Status == LockerBoxStatus.Available)
                    .GroupBy(b => b.LockerId).Select(g => new { LockerId = g.Key, Count = g.Count() })))
                .ToDictionary(x => x.LockerId, x => x.Count);
            foreach (var dto in lockers)
            {
                dto.EmptyBoxesCount = empty.GetValueOrDefault(dto.Id);
                dto.ImageUrls = images.GetValueOrDefault(dto.Id) ?? [];
                dto.Merchants = links.Where(x => x.LockerId == dto.Id).OrderBy(x => x.Priority)
                    .Select(x => new LockerMerchantDto
                    {
                        MerchantId = x.MerchantId,
                        BusinessName = merchants.GetValueOrDefault(x.MerchantId, string.Empty),
                        Priority = x.Priority
                    }).ToList();
                dto.ServiceAreaName = dto.ServiceAreaId.HasValue ? areas.GetValueOrDefault(dto.ServiceAreaId.Value) : null;
            }
        }

        private static List<string> ReadImages(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return [];
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        public async Task<BaseResponse<LockerSummaryDto>> GetSummaryAsync(Guid? merchantId)
        {
            var scope = ScopeMerchantId ?? merchantId;
            // Both counts in one repeatable-read transaction so lockers and boxes describe the same moment.
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true,
                isolationLevel: System.Data.IsolationLevel.RepeatableRead);
            var links = await _linkRepo.GetQueryableAsync();
            var lockers = (await _lockerRepo.GetQueryableAsync()).Where(x => !x.IsDeleted);
            if (scope.HasValue)
                lockers = lockers.Where(x => links.Any(l => l.LockerId == x.Id && l.MerchantId == scope.Value));
            var byStatus = await AsyncExecuter.ToListAsync(lockers.GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() }));
            var lockerIds = lockers.Select(x => x.Id);
            var boxes = await AsyncExecuter.ToListAsync((await _boxRepo.GetQueryableAsync())
                .Where(b => !b.IsDeleted && lockerIds.Contains(b.LockerId))
                .GroupBy(b => b.Status).Select(g => new { Status = g.Key, Count = g.Count() }));
            await uow.CompleteAsync();
            int L(LockerStatus s) => byStatus.Where(x => x.Status == s).Sum(x => x.Count);
            int B(LockerBoxStatus s) => boxes.Where(x => x.Status == s).Sum(x => x.Count);
            return Success(new LockerSummaryDto
            {
                TotalLockers = byStatus.Sum(x => x.Count),
                ActiveLockers = L(LockerStatus.Active),
                MaintenanceLockers = L(LockerStatus.Maintenance),
                InactiveLockers = L(LockerStatus.Inactive),
                TotalBoxes = boxes.Sum(x => x.Count),
                AvailableBoxes = B(LockerBoxStatus.Available),
                OccupiedBoxes = B(LockerBoxStatus.Occupied),
                ReservedBoxes = B(LockerBoxStatus.Reserved),
                MaintenanceBoxes = B(LockerBoxStatus.Maintenance),
                GeneratedAt = DateTime.UtcNow
            });
        }

        private static PaginatedResult<LockerUsageDto> UsageError(System.Net.HttpStatusCode status, string message) => new()
        {
            Succeeded = false, StatusCode = status, Message = message
        };

        public async Task<PaginatedResult<LockerUsageDto>> GetUsageHistoryAsync(Guid id, LockerUsageQuery query)
        {
            const long maxUnix = BaseFilterQuery.MaxUnixSeconds;
            if (query.FromDate is < 0 or > maxUnix || query.ToDate is < 0 or > maxUnix)
                return UsageError(System.Net.HttpStatusCode.BadRequest, "Khoảng ngày không hợp lệ (fromDate/toDate là Unix giây, không âm).");
            if (query.FromDate is > 0 && query.ToDate is > 0 && query.FromDate > query.ToDate)
                return UsageError(System.Net.HttpStatusCode.BadRequest, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
            var locker = await _lockerRepo.FindAsync(id);
            if (locker == null || locker.IsDeleted || !await InScopeAsync(locker.Id))
                return UsageError(System.Net.HttpStatusCode.NotFound, "Không tìm thấy thông tin tủ Locker!");
            // A box of another locker is an input error, not an empty history; deleted boxes keep their history.
            if (query.BoxId.HasValue && !await _boxRepo.AnyAsync(x => x.Id == query.BoxId.Value && x.LockerId == id))
                return UsageError(System.Net.HttpStatusCode.BadRequest, "Ô tủ không thuộc locker này.");

            var orderQuery = (await _orderRepo.GetQueryableAsync())
                .Where(x => !x.IsDeleted && ((x.LockerId == id && x.DepositedAt != null) ||
                                             (x.ReturnLockerId == id && x.ReturnBoxNumber != null)));
            // Shared lockers: a Merchant only sees the uses of its own orders.
            if (ScopeMerchantId.HasValue)
                orderQuery = orderQuery.Where(x => x.MerchantId == ScopeMerchantId.Value);
            if (query.BoxId.HasValue)
                orderQuery = orderQuery.Where(x => x.BoxId == query.BoxId.Value || x.ReturnBoxId == query.BoxId.Value);
            var orders = await AsyncExecuter.ToListAsync(orderQuery);
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
                        UsageId = $"{o.Id}:1", BoxId = o.BoxId,
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
                        UsageId = $"{o.Id}:2", BoxId = o.ReturnBoxId,
                        OrderId = o.Id, OrderCode = o.OrderCode, BoxNumber = o.ReturnBoxNumber, UsageType = 2,
                        PutInAt = putIn,
                        TakenOutAt = cancelledReturn ? o.CancelledGoodsReturnedAt
                            : At(o.Id, WashOrderStatus.Returned) ?? o.MovedToStorageAt,
                        OrderStatus = o.Status
                    });
                }
            }

            IEnumerable<LockerUsageDto> filtered = rows;
            if (query.BoxId.HasValue)
                filtered = filtered.Where(x => x.BoxId == query.BoxId.Value);
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
            // Stable order across pages: time, then order code, then usage type (ties and missing times included).
            var list = filtered.OrderByDescending(x => x.PutInAt ?? DateTime.MinValue)
                .ThenBy(x => x.OrderCode, StringComparer.Ordinal).ThenBy(x => x.UsageType).ToList();
            var page = Math.Max(1, query.Page);
            var take = Math.Clamp(query.Take, 1, 100);
            return Success(list.Skip((page - 1) * take).Take(take).ToList(), list.Count, page, take,
                list.Count == 0 ? "Locker này chưa được sử dụng." : null);
        }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, LockerFilterQuery query)
        {
            var merchantId = ScopeMerchantId;
            var links = await _linkRepo.GetQueryableAsync();
            var result = await _lockerRepo.GetColumnDistinctValuesAsync(fieldName, query,
                merchantId.HasValue ? q => q.Where(x => links.Any(l => l.LockerId == x.Id && l.MerchantId == merchantId.Value)) : null);
            return Success(result);
        }
    }
}
