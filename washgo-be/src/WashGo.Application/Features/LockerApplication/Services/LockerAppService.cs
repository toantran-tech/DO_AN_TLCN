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
using WashGo.Domain.Shared.Validation;
using WashGo.Domain.Entities.SystemConfig;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Features.AuditApplication.Services;
using System.Data;
using System.Net;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;

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
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<LockerResultDto>(command.OperationId, CurrentUser.Id!.Value, "Locker", "Create", "new", command) is { } replay) return replay;
            await Writes.LockAsync("locker-admin-names");
            if (await ValidateAsync(command, null) is { } invalid)
                return invalid;
            var (imageError, images) = await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), command.ImageUrls, CurrentUser.Id!.Value, 0);
            if (imageError != null)
                return BadRequest<LockerResultDto>(imageError);

            if (command.Boxes == null || command.Boxes.Count > MaxBoxes || command.BoxCount is < 1 or > MaxBoxes) return BadRequest<LockerResultDto>("Số ô phải từ 1 đến 200.");
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

            // A code sent by an older client is ignored; the claim keeps codes of deleted lockers in use.
            var locker = new Locker(GuidGenerator.Create());
            locker.Code = await LazyServiceProvider.LazyGetRequiredService<IBusinessCodeAllocator>().AllocateAsync(BusinessCodeRules.Locker, locker.Id);
            locker.ServiceAreaId = command.ServiceAreaId;
            locker.Name = command.Name.Trim();
            locker.Address = command.Address.Trim();
            locker.Ward = string.IsNullOrWhiteSpace(command.Ward) ? null : command.Ward.Trim();
            locker.District = command.District?.Trim() ?? string.Empty;
            locker.City = command.City?.Trim() ?? string.Empty;
            locker.Latitude = command.Latitude;
            locker.Longitude = command.Longitude;
            locker.Status = LockerStatus.Active;
            locker.TotalBoxes = boxes.Count;
            locker.AvailableBoxes = boxes.Count;
            locker.LockerType = command.LockerType;
            locker.Description = command.Description;
            locker.Images = images.Count == 0 ? null : JsonSerializer.Serialize(images);

            foreach (var b in boxes)
            {
                locker.Boxes.Add(new LockerBox
                {
                    BoxNumber = b.BoxNumber,
                    Size = b.Size,
                    Status = LockerBoxStatus.Available
                });
            }

            await _lockerRepo.InsertAsync(locker, autoSave: true);
            await ReplaceMerchantsAsync(locker.Id, MerchantIdsOf(command));
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            await FillNamesAsync([dto]);
            dto.ImageUrls = images;
            var response = Created(dto, "Thêm Locker thành công!");
            await Audit.LogAsync(new AuditEntry("Locker", "Create", "Locker", locker.Id.ToString(), locker.Code));
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Locker", "Create", "new", command, response);
            await uow.CompleteAsync(); return response;
        }

        // UC 3.4.3
        public async Task<BaseResponse<LockerResultDto>> UpdateAsync(UpdateLockerCommand command)
        {
            if (!Enum.IsDefined(command.Status))
                return BadRequest<LockerResultDto>("Trạng thái tủ không hợp lệ.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<LockerResultDto>(command.OperationId, CurrentUser.Id!.Value, "Locker", "Update", command.Id.ToString(), command) is { } replay) return replay;
            await Writes.LockAsync("locker-admin-names");
            await _lockerRepo.LockAsync(command.Id);
            var locker = await _lockerRepo.GetWithBoxesAsync(command.Id);
            if (locker == null)
            {
                return NotFound<LockerResultDto>("Không tìm thấy trạm Locker để cập nhật!");
            }
            var version = await VersionAsync(locker);
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<LockerResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != version) return AdministrativeWriteGuard.Error<LockerResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Locker đã thay đổi; hãy tải lại.");
            if (!BusinessCodeRules.AcceptsOnUpdate(command.Code, locker.Code))
                return Invalid(nameof(command.Code), BusinessCodeRules.ImmutableMessage, BusinessCodeRules.ImmutableCode);
            if (await ValidateAsync(command, locker.Id) is { } invalid)
                return invalid;
            var images = ReadImages(locker.Images);
            if (command.ImageUrls != null)
            {
                // Images already on the locker stay valid; new ones must be the caller's uploads.
                var added = command.ImageUrls.Where(x => !images.Contains(x?.Trim() ?? string.Empty)).ToList();
                var imageError = (await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), added, CurrentUser.Id!.Value, 0)).Error;
                if (imageError != null)
                    return BadRequest<LockerResultDto>(imageError);
                images = command.ImageUrls.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
                if (images.Count > UploadStorage.MaxImagesPerSubmission)
                    return BadRequest<LockerResultDto>($"Tối đa {UploadStorage.MaxImagesPerSubmission} ảnh.");
            }

            var expectedStatus = locker.Status;
            locker.Name = command.Name.Trim();
            locker.Address = command.Address.Trim();
            // Ward/District are optional now: a form that does not send them keeps the stored (legacy) values.
            if (command.Ward != null) locker.Ward = string.IsNullOrWhiteSpace(command.Ward) ? null : command.Ward.Trim();
            if (command.District != null) locker.District = command.District.Trim();
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
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            await FillNamesAsync([dto]);
            dto.ImageUrls = images;
            var response = Success(dto, "Cập nhật Locker thành công!");
            await Audit.LogAsync(new AuditEntry("Locker", "Update", "Locker", locker.Id.ToString(), locker.Code));
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Locker", "Update", command.Id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        // UC 3.4.4: soft delete the locker and its boxes; blocked while any unfinished order uses it.
        public async Task<BaseResponse<bool>> DeleteAsync(Guid id, string? expectedVersion = null, Guid? operationId = null)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var payload = new { id, expectedVersion };
            if (await Writes.ReplayAsync<bool>(operationId, CurrentUser.Id!.Value, "Locker", "Delete", id.ToString(), payload) is { } replay) return replay;
            await _lockerRepo.LockAsync(id);
            var locker = await _lockerRepo.GetWithBoxesAsync(id);
            if (locker == null)
                return NotFound<bool>("Không tìm thấy thông tin tủ Locker!");
            if (string.IsNullOrWhiteSpace(expectedVersion)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (expectedVersion != await VersionAsync(locker)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.Conflict, "VersionMismatch", "Locker đã thay đổi.");
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
            var response = Success(true, "Xóa Locker thành công!");
            await Audit.LogAsync(new AuditEntry("Locker", "Delete", "Locker", id.ToString(), locker.Code));
            await Writes.RecordAsync(operationId, CurrentUser.Id!.Value, "Locker", "Delete", id.ToString(), payload, response);
            await uow.CompleteAsync(); return response;
        }

        // UC 3.4.5: Admin only moves a box between Trống and Bảo trì; boxes holding goods cannot go to maintenance.
        public async Task<BaseResponse<bool>> UpdateBoxStatusAsync(UpdateLockerBoxStatusCommand command)
        {
            if (command.NewStatus is not (LockerBoxStatus.Available or LockerBoxStatus.Maintenance))
                return BadRequest<bool>("Chỉ có thể chuyển ô tủ sang Trống hoặc Bảo trì.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<bool>(command.OperationId, CurrentUser.Id!.Value, "Locker", "BoxStatus", command.BoxId.ToString(), command) is { } replay) return replay;
            await _lockerRepo.LockAsync(command.LockerId);
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
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion của locker.");
            if (command.ExpectedVersion != await VersionAsync(locker)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.Conflict, "VersionMismatch", "Locker đã thay đổi.");
            // The locker version does not cover box states: the box state the Admin saw is checked under the same locker lock
            // that reservations use, so a change in between (other Admin, reservation) is a conflict, never applied blindly.
            if (!command.ExpectedBoxStatus.HasValue)
                return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.BadRequest, "ExpectedBoxStatusRequired", "Thiếu expectedBoxStatus (trạng thái ô lúc bạn xem).");
            if (box.Status != command.ExpectedBoxStatus.Value)
                return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.Conflict, "BoxStatusChanged", $"Ô tủ đã chuyển sang {box.Status}; hãy tải lại.");

            if (box.Status == command.NewStatus)
            {
                var unchanged = AdministrativeBatchExecutor.Unchanged(Success(true, "Ô tủ đã ở trạng thái yêu cầu."));
                await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Locker", "BoxStatus", command.BoxId.ToString(), command, unchanged);
                await uow.CompleteAsync(); return unchanged;
            }
            if (box.Status is LockerBoxStatus.Occupied or LockerBoxStatus.Reserved)
                return BadRequest<bool>("Ô tủ đang có đồ hoặc đang được giữ chỗ, không thể chuyển sang Bảo trì.");
            if (!await _lockerRepo.TrySetUnassignedBoxStatusAsync(locker.Id, box.Id,
                    box.Status, command.NewStatus))
                return BadRequest<bool>("Ô tủ đang gắn với đơn hàng hoặc vừa đổi trạng thái; không thể cập nhật trực tiếp.");
            var response = Success(true, "Cập nhật ô tủ thành công!");
            await Audit.LogAsync(new AuditEntry("Locker", "BoxStatus", "LockerBox", box.Id.ToString(), locker.Code) { Details = new { command.NewStatus } });
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Locker", "BoxStatus", command.BoxId.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        /// <summary>One field error: message, errors[field] and meta.code (ValidationFailed unless given).</summary>
        private static BaseResponse<LockerResultDto> Invalid(string field, string message, string code = "ValidationFailed") =>
            Invalid(new Dictionary<string, string[]> { [Camel(field)] = [message] }, code);

        private static BaseResponse<LockerResultDto> Invalid(Dictionary<string, string[]> errors, string code = "ValidationFailed") => new()
        {
            Succeeded = false, StatusCode = HttpStatusCode.BadRequest, Message = errors.First().Value[0],
            Errors = errors, Meta = new { code, fields = errors.Keys.ToArray() }
        };

        private static string Camel(string name) => char.ToLowerInvariant(name[0]) + name[1..];

        /// <summary>
        /// Field-by-field validation: every invalid input field is reported under its own key (name, address, city,
        /// district, ward, description, latitude/longitude, lockerType, merchantIds, serviceAreaId). Code, Ward and
        /// District are not required.
        /// </summary>
        private async Task<BaseResponse<LockerResultDto>?> ValidateAsync(CreateLockerCommand command, Guid? currentId)
        {
            var errors = new Dictionary<string, string[]>();
            void Add(string field, string message) => errors.TryAdd(Camel(field), [message]);
            var name = command.Name?.Trim() ?? string.Empty;
            if (name.Length is 0 or > 200)
                Add(nameof(command.Name), "Tên locker phải có từ 1 đến 200 ký tự.");
            if (string.IsNullOrWhiteSpace(command.Address) || command.Address.Trim().Length > 500)
                Add(nameof(command.Address), "Địa chỉ không được để trống (tối đa 500 ký tự).");
            if (string.IsNullOrWhiteSpace(command.City) || command.City.Trim().Length > 100)
                Add(nameof(command.City), "Tỉnh/thành không được để trống (tối đa 100 ký tự).");
            if (command.District?.Trim().Length > 100)
                Add(nameof(command.District), "Quận/huyện tối đa 100 ký tự.");
            if (command.Ward?.Trim().Length > 100)
                Add(nameof(command.Ward), "Phường/xã tối đa 100 ký tự.");
            if (command.Description?.Length > 2000)
                Add(nameof(command.Description), "Mô tả tối đa 2000 ký tự.");
            if (double.IsNaN(command.Latitude) || command.Latitude is < -90 or > 90 || command.Latitude == 0 && command.Longitude == 0)
                Add(nameof(command.Latitude), "Vĩ độ không hợp lệ; vui lòng xác nhận vị trí trên bản đồ.");
            if (double.IsNaN(command.Longitude) || command.Longitude is < -180 or > 180 || command.Latitude == 0 && command.Longitude == 0)
                Add(nameof(command.Longitude), "Kinh độ không hợp lệ; vui lòng xác nhận vị trí trên bản đồ.");
            if (!Enum.IsDefined(command.LockerType))
                Add(nameof(command.LockerType), "Loại locker không hợp lệ.");
            // Several Merchants may share a locker; the Admin picks one per order (UC 3.3).
            var merchantIds = MerchantIdsOf(command);
            if (currentId == null || merchantIds.Count > 0 || command.MerchantIds != null)
            {
                if (merchantIds.Count == 0)
                    Add(nameof(command.MerchantIds), "Vui lòng chọn ít nhất một Merchant phụ trách locker.");
                else if (merchantIds.Distinct().Count() != merchantIds.Count)
                    Add(nameof(command.MerchantIds), "Mỗi Merchant chỉ được chọn một lần.");
                else if (await AsyncExecuter.CountAsync((await _merchantRepo.GetQueryableAsync())
                    .Where(x => merchantIds.Contains(x.Id) && x.Status == MerchantStatus.Active)) != merchantIds.Count)
                    Add(nameof(command.MerchantIds), "Merchant phụ trách không tồn tại hoặc không hoạt động.");
            }
            if (command.ServiceAreaId.HasValue && !await _areaRepo.AnyAsync(x => x.Id == command.ServiceAreaId.Value && x.IsActive))
                Add(nameof(command.ServiceAreaId), "Khu vực không tồn tại hoặc đã ngừng hoạt động.");
            var normalized = name.ToLower();
            if (name.Length > 0 && await _lockerRepo.AnyAsync(x => !x.IsDeleted && x.Id != currentId && x.Name.ToLower() == normalized))
                Add(nameof(command.Name), "Tên locker này đã được sử dụng.");
            return errors.Count == 0 ? null : Invalid(errors);
        }
        public async Task<BaseResponse<LockerResultDto>> SetStatusAsync(Guid id, SetLockerStatusCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<LockerResultDto>(command.OperationId, CurrentUser.Id!.Value, "Locker", "Status", id.ToString(), command) is { } replay) return replay;
            await _lockerRepo.LockAsync(id);
            var locker = await _lockerRepo.GetWithBoxesAsync(id);
            if (locker == null) return NotFound<LockerResultDto>("Không tìm thấy locker.");
            if (!Enum.IsDefined(command.Status) || string.IsNullOrWhiteSpace(command.ExpectedVersion)) return BadRequest<LockerResultDto>("Cần trạng thái hợp lệ và expectedVersion.");
            if (command.ExpectedVersion != await VersionAsync(locker)) return AdministrativeWriteGuard.Error<LockerResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Locker đã thay đổi.");
            var old = locker.Status; locker.Status = command.Status;
            if (!await _lockerRepo.TryUpdateDetailsAsync(locker, old)) return BadRequest<LockerResultDto>("Locker đang có đồ/giữ chỗ; chưa thể đổi trạng thái.");
            if (old != command.Status) await Audit.LogAsync(new AuditEntry("Locker", "Status", "Locker", id.ToString(), locker.Code) { ChangedFields = ["status"] });
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker); await FillNamesAsync([dto]);
            var response = Success(dto);
            if (old == command.Status) AdministrativeBatchExecutor.Unchanged(response);
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Locker", "Status", id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(AdministrativeBatchCommand command) =>
            LazyServiceProvider.LazyGetRequiredService<AdministrativeBatchExecutor>().RunAsync("Locker", command, 50, async (item, key) =>
            {
                if (command.Action == "Delete") return AdministrativeBatchExecutor.Item(item.Id, await DeleteAsync(item.Id, item.ExpectedVersion, key));
                if (!command.Status.HasValue || !Enum.IsDefined((LockerStatus)command.Status.Value)) return AdministrativeBatchExecutor.Invalid(item.Id, "InvalidTargetStatus", "Status đích không hợp lệ.");
                return AdministrativeBatchExecutor.Item(item.Id, await SetStatusAsync(item.Id, new() { ExpectedVersion = item.ExpectedVersion, OperationId = key, Status = (LockerStatus)command.Status.Value }));
            });

        private static List<Guid> MerchantIdsOf(CreateLockerCommand command) =>
            command.MerchantIds is { Count: > 0 } ids ? ids
            : command.MerchantId.HasValue ? [command.MerchantId.Value] : [];

        // Merchants only see lockers they are assigned to (UC 2.5); Admin sees all.
        private async Task<bool> InScopeAsync(Guid lockerId) =>
            !ScopeMerchantId.HasValue ||
            await _linkRepo.AnyAsync(x => x.LockerId == lockerId && x.MerchantId == ScopeMerchantId.Value);

        private async Task ReplaceMerchantsAsync(Guid lockerId, List<Guid> merchantIds)
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

        private async Task FillNamesAsync(List<LockerResultDto> lockers)
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
            var entities = await _lockerRepo.GetListAsync(x => lockerIds.Contains(x.Id));
            var pending = (await _orderRepo.GetQueryableAsync()).Where(o => !o.IsDeleted &&
                (o.Status == WashOrderStatus.Cancelled ? o.CancelledGoodsStatus == CancelledGoodsStatus.AtDepositLocker || o.CancelledGoodsStatus == CancelledGoodsStatus.AtReturnLocker :
                    o.Status != WashOrderStatus.Completed && o.Status != WashOrderStatus.MovedToStorage));
            var blocking = await AsyncExecuter.ToListAsync(pending.Where(o => lockerIds.Contains(o.LockerId)).Select(o => o.LockerId)
                .Concat(pending.Where(o => o.ReturnLockerId.HasValue && lockerIds.Contains(o.ReturnLockerId.Value)).Select(o => o.ReturnLockerId!.Value)).Distinct());
            // Projected list rows do not load boxes, so count empty boxes here (UC 3.4.1 "Số ô trống").
            var empty = (await AsyncExecuter.ToListAsync((await _boxRepo.GetQueryableAsync())
                    .Where(b => lockerIds.Contains(b.LockerId) && !b.IsDeleted && b.Status == LockerBoxStatus.Available)
                    .GroupBy(b => b.LockerId).Select(g => new { LockerId = g.Key, Count = g.Count() })))
                .ToDictionary(x => x.LockerId, x => x.Count);
            foreach (var dto in lockers)
            {
                var entity = entities.First(x => x.Id == dto.Id);
                dto.Version = Version(entity, links.Where(x => x.LockerId == dto.Id));
                dto.CanDelete = CurrentUser.IsInRole("Admin") && entity.AvailableBoxes == entity.TotalBoxes && !blocking.Contains(dto.Id);
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

        private async Task<string> VersionAsync(Locker locker) => Version(locker, await _linkRepo.GetListAsync(x => x.LockerId == locker.Id));
        private static string Version(Locker x, IEnumerable<LockerMerchant> links) => AdministrativeWriteGuard.Version(new
        {
            x.Id, x.Name, x.Code, x.Address, x.Ward, x.District, x.City, x.Latitude, x.Longitude, x.Status, x.LockerType,
            x.Description, x.ServiceAreaId, Images = ReadImages(x.Images), x.IsDeleted,
            Merchants = links.OrderBy(l => l.Priority).ThenBy(l => l.MerchantId).Select(l => new { l.MerchantId, l.Priority })
        });

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
                isolationLevel: IsolationLevel.RepeatableRead);
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

        private static PaginatedResult<LockerUsageDto> UsageError(HttpStatusCode status, string message) => new()
        {
            Succeeded = false, StatusCode = status, Message = message
        };

        public async Task<PaginatedResult<LockerUsageDto>> GetUsageHistoryAsync(Guid id, LockerUsageQuery query)
        {
            const long maxUnix = BaseFilterQuery.MaxUnixSeconds;
            if (query.FromDate is < 0 or > maxUnix || query.ToDate is < 0 or > maxUnix)
                return UsageError(HttpStatusCode.BadRequest, "Khoảng ngày không hợp lệ (fromDate/toDate là Unix giây, không âm).");
            if (query.FromDate is > 0 && query.ToDate is > 0 && query.FromDate > query.ToDate)
                return UsageError(HttpStatusCode.BadRequest, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
            var locker = await _lockerRepo.FindAsync(id);
            if (locker == null || locker.IsDeleted || !await InScopeAsync(locker.Id))
                return UsageError(HttpStatusCode.NotFound, "Không tìm thấy thông tin tủ Locker!");
            // A box of another locker is an input error, not an empty history; deleted boxes keep their history.
            if (query.BoxId.HasValue && !await _boxRepo.AnyAsync(x => x.Id == query.BoxId.Value && x.LockerId == id))
                return UsageError(HttpStatusCode.BadRequest, "Ô tủ không thuộc locker này.");

            var page = Math.Max(1, query.Page); var take = Math.Clamp(query.Take, 1, 100);
            DateTime? from = query.FromDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(query.FromDate.Value).UtcDateTime : null;
            DateTime? to = query.ToDate is > 0 ? DateTimeOffset.FromUnixTimeSeconds(query.ToDate.Value).UtcDateTime : null;
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
            var (rows, total) = await LazyServiceProvider.LazyGetRequiredService<ILockerUsageReader>().ReadAsync(id, ScopeMerchantId, query.BoxId, from, to, page, take);
            var dtos = rows.Select(x => new LockerUsageDto { UsageId = $"{x.OrderId}:{x.UsageType}", OrderId = x.OrderId, OrderCode = x.OrderCode,
                BoxId = x.BoxId, BoxNumber = x.BoxNumber, UsageType = x.UsageType, PutInAt = x.PutInAt, TakenOutAt = x.TakenOutAt, OrderStatus = (WashOrderStatus)x.OrderStatus }).ToList();
            await snapshot.CompleteAsync();
            return Success(dtos, total, page, take, total == 0 ? "Locker này chưa được sử dụng." : null);
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
