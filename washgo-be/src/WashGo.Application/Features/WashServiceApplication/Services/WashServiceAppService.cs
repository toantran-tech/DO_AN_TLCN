using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using System.Text.Json;
using System.Net;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Application.Features.FileApplication.Services;
using Microsoft.Extensions.Configuration;
using WashGo.Application.Contracts.Features.WashServiceContracts.Commands;
using WashGo.Application.Contracts.Features.WashServiceContracts.Queries;
using WashGo.Application.Contracts.Features.WashServiceContracts.Results;
using WashGo.Application.Contracts.Features.WashServiceContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;
using System.Data;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;
using WashGo.Application.Contracts.Features.AuditContracts.Results;

namespace WashGo.Application.Features.WashServiceApplication.Services
{
    public class WashServiceAppService : WashGoAppService, IWashServiceAppService
    {
        private readonly IWashServiceRepository _serviceRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IWashOrderRepository _orderRepo;

        public WashServiceAppService(
            IWashServiceRepository serviceRepo,
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<WashOrderItem, Guid> itemRepo,
            IWashOrderRepository orderRepo)
        {
            _serviceRepo = serviceRepo;
            _merchantRepo = merchantRepo;
            _itemRepo = itemRepo;
            _orderRepo = orderRepo;
        }

        // UC 3.5.4: soft delete; blocked while any unfinished order uses the service.
        public async Task<BaseResponse<bool>> DeleteAsync(Guid id, string? expectedVersion = null, Guid? operationId = null)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var payload = new { id, expectedVersion };
            if (await Writes.ReplayAsync<bool>(operationId, CurrentUser.Id!.Value, "Service", "Delete", id.ToString(), payload) is { } replay) return replay;
            await _serviceRepo.AcquireLockAsync("service-catalog");
            var entity = await _serviceRepo.FindAsync(id);
            if (entity == null || entity.IsDeleted)
                return NotFound<bool>("Không tìm thấy dịch vụ!");
            if (string.IsNullOrWhiteSpace(expectedVersion)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (expectedVersion != Version(entity)) return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.Conflict, "VersionMismatch", "Dịch vụ đã thay đổi.");
            var items = await _itemRepo.GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var inUse = await AsyncExecuter.AnyAsync(
                from item in items
                join order in orders on item.WashOrderId equals order.Id
                where item.ServiceId == id && !order.IsDeleted &&
                      order.Status != WashOrderStatus.Completed && order.Status != WashOrderStatus.Cancelled
                select item.Id);
            if (inUse)
                return AdministrativeWriteGuard.Error<bool>(HttpStatusCode.BadRequest, "InUseByOrders", "Không thể xóa dịch vụ đang có đơn hàng.");

            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.DeletedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            entity.DeletedBy = CurrentUser.Id?.ToString();
            await _serviceRepo.UpdateAsync(entity, autoSave: true);
            var response = Success(true, "Xóa Dịch vụ thành công!");
            await Audit.LogAsync(new AuditEntry("Service", "Delete", "WashService", id.ToString(), entity.Code));
            await Writes.RecordAsync(operationId, CurrentUser.Id!.Value, "Service", "Delete", id.ToString(), payload, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<PaginatedResult<WashServiceResultDto>> GetListAsync(WashServiceFilterQuery query)
        {
            var (data, total) = await _serviceRepo.FilterProjectedDataAsync<WashServiceResultDto>(
                filterFunc: q =>
                {
                    q = q.Where(x => !x.IsDeleted);
                    if (query.MerchantId.HasValue)
                        q = q.Where(x => x.MerchantId == query.MerchantId.Value);
                    if (query.ApprovalStatus.HasValue)
                        q = q.Where(x => x.ApprovalStatus == query.ApprovalStatus.Value);
                    if (query.ServiceType.HasValue)
                        q = q.Where(x => x.ServiceType == query.ServiceType.Value);
                    if (query.IsActive.HasValue)
                        q = q.Where(x => x.IsActive == query.IsActive.Value);
                    if (!string.IsNullOrWhiteSpace(query.Keyword))
                    {
                        var kw = query.Keyword.Trim().ToLower();
                        q = q.Where(x => x.Code.ToLower().Contains(kw) || x.Name.ToLower().Contains(kw));
                    }
                    return q;
                },
                parameters: query,
                defaultOrder: q => q.OrderBy(x => x.Code).ThenBy(x => x.Id)
            );

            var ids = data.Select(x => x.Id).ToList();
            var entities = await _serviceRepo.GetListAsync(x => ids.Contains(x.Id));
            foreach (var dto in data) Fill(dto, entities.First(x => x.Id == dto.Id));
            await FillActionsAsync(data.ToList());
            return Success(data, total, query.Page, query.Take, total == 0 ? "Chưa có dịch vụ nào." : null);
        }

        public async Task<BaseResponse<WashServiceResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await _serviceRepo.FindAsync(id);
            if (entity == null || entity.IsDeleted)
            {
                return NotFound<WashServiceResultDto>("Không tìm thấy dịch vụ!");
            }

            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            Fill(dto, entity);
            await FillActionsAsync([dto]);
            return Success(dto);
        }
        public async Task<BaseResponse<AdministrativeSummaryDto>> GetSummaryAsync()
        {
            // Service counts and pending proposals come from one repeatable-read snapshot.
            using var snapshot = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true, isolationLevel: IsolationLevel.RepeatableRead);
            var groups = await AsyncExecuter.ToListAsync((await _serviceRepo.GetQueryableAsync()).Where(x => !x.IsDeleted)
                .GroupBy(x => new { x.ApprovalStatus, x.IsActive }).Select(g => new { g.Key.ApprovalStatus, g.Key.IsActive, Count = g.Count() }));
            var pending = await AsyncExecuter.CountAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<ServiceChangeRequest, Guid>>()
                .GetQueryableAsync()).Where(x => x.Status == ServiceApprovalStatus.Pending));
            await snapshot.CompleteAsync();
            var counts = groups.ToDictionary(x => $"{x.ApprovalStatus}:{(x.IsActive ? "Active" : "Inactive")}", x => x.Count);
            counts["PendingProposals"] = pending;
            return Success(new AdministrativeSummaryDto { Total = groups.Sum(x => x.Count), Counts = counts, GeneratedAt = DateTime.UtcNow, PendingProposals = pending });
        }

        /// <summary>Action metadata from the same rule DeleteAsync applies (active orders block deletion); re-checked at commit.</summary>
        private async Task FillActionsAsync(List<WashServiceResultDto> dtos)
        {
            if (dtos.Count == 0) return;
            var ids = dtos.Select(x => x.Id).ToList();
            var inUse = (await AsyncExecuter.ToListAsync(
                from item in await _itemRepo.GetQueryableAsync()
                join order in await _orderRepo.GetQueryableAsync() on item.WashOrderId equals order.Id
                where ids.Contains(item.ServiceId) && !order.IsDeleted && order.Status != WashOrderStatus.Completed && order.Status != WashOrderStatus.Cancelled
                select item.ServiceId)).ToHashSet();
            var pendingIds = (await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<ServiceChangeRequest, Guid>>()
                .GetQueryableAsync()).Where(x => x.Status == ServiceApprovalStatus.Pending && x.ServiceId != null && ids.Contains(x.ServiceId.Value))
                .Select(x => x.ServiceId!.Value))).ToHashSet();
            foreach (var dto in dtos)
            {
                dto.CanEdit = true;
                dto.CanChangeAvailability = true;
                dto.CanDelete = !inUse.Contains(dto.Id);
                dto.DeleteReasonCode = dto.CanDelete ? null : "InUseByOrders";
                dto.HasPendingProposal = pendingIds.Contains(dto.Id);
            }
        }

        private async Task<string?> ValidateAsync(CreateWashServiceCommand command, Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(command.Code))
                return "Mã dịch vụ không được để trống.";
            if (command.Code.Trim().Length > 50 || command.Description?.Length > 2000) return "Mã tối đa 50, mô tả 2000 ký tự.";
            var error = WashServiceRules.Validate(command.Name, command.UnitPrice, command.PricePerKg, command.PricePerItem,
                command.EstimatedDurationHours, command.ServiceType, command.Unit);
            if (error != null)
                return error;
            if (command.MerchantId.HasValue && await _merchantRepo.FindAsync(command.MerchantId.Value) == null)
                return "Merchant đề xuất không tồn tại.";
            if (await _serviceRepo.AnyAsync(x => x.Code == command.Code && x.Id != currentId))
                return $"Mã dịch vụ '{command.Code}' đã tồn tại.";
            if (await WashServiceRules.NameTakenAsync(_serviceRepo, command.Name, currentId))
                return WashServiceRules.DuplicateName;
            return null;
        }

        public async Task<BaseResponse<WashServiceResultDto>> CreateAsync(CreateWashServiceCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<WashServiceResultDto>(command.OperationId, CurrentUser.Id!.Value, "Service", "Create", "new", command) is { } replay) return replay;
            await _serviceRepo.AcquireLockAsync("service-catalog");
            var error = await ValidateAsync(command, null);
            if (error != null)
                return BadRequest<WashServiceResultDto>(error);

            var entity = ObjectMapper.Map<CreateWashServiceCommand, WashService>(command);
            var imageError = await ApplyImagesAsync(entity, command.ImageUrls);
            if (imageError != null) return BadRequest<WashServiceResultDto>(imageError);
            entity.ApprovalStatus = ServiceApprovalStatus.Approved;
            entity.IsActive = true;
            await _serviceRepo.InsertAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            Fill(dto, entity);
            var response = Created(dto, "Thêm Dịch vụ thành công!");
            await Audit.LogAsync(new AuditEntry("Service", "Create", "WashService", entity.Id.ToString(), entity.Code));
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Service", "Create", "new", command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<WashServiceResultDto>> UpdateAsync(UpdateWashServiceCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<WashServiceResultDto>(command.OperationId, CurrentUser.Id!.Value, "Service", "Update", command.Id.ToString(), command) is { } replay) return replay;
            await _serviceRepo.AcquireLockAsync("service-catalog");
            var entity = await _serviceRepo.FindAsync(command.Id);
            if (entity == null || entity.IsDeleted)
            {
                return NotFound<WashServiceResultDto>("Không tìm thấy dịch vụ để cập nhật!");
            }
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<WashServiceResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != Version(entity)) return AdministrativeWriteGuard.Error<WashServiceResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Dịch vụ đã thay đổi.");

            var error = await ValidateAsync(command, command.Id);
            if (error != null)
                return BadRequest<WashServiceResultDto>(error);

            ObjectMapper.Map(command, entity);
            var imageError = await ApplyImagesAsync(entity, command.ImageUrls);
            if (imageError != null) return BadRequest<WashServiceResultDto>(imageError);
            await _serviceRepo.UpdateAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            Fill(dto, entity);
            var response = Success(dto, "Cập nhật Dịch vụ thành công!");
            await Audit.LogAsync(new AuditEntry("Service", "Update", "WashService", entity.Id.ToString(), entity.Code));
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Service", "Update", command.Id.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        public async Task<BaseResponse<WashServiceResultDto>> UpdateStatusAsync(UpdateWashServiceStatusCommand command)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            if (await Writes.ReplayAsync<WashServiceResultDto>(command.OperationId, CurrentUser.Id!.Value, "Service", "Status", command.ServiceId.ToString(), command) is { } replay) return replay;
            await _serviceRepo.AcquireLockAsync("service-catalog");
            var entity = await _serviceRepo.FindAsync(command.ServiceId);
            if (entity == null || entity.IsDeleted)
            {
                return NotFound<WashServiceResultDto>("Không tìm thấy dịch vụ!");
            }
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion)) return AdministrativeWriteGuard.Error<WashServiceResultDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != Version(entity)) return AdministrativeWriteGuard.Error<WashServiceResultDto>(HttpStatusCode.Conflict, "VersionMismatch", "Dịch vụ đã thay đổi.");
            if (!Enum.IsDefined(command.ApprovalStatus) || command.RejectionReason?.Length > 1000 || command.ApprovalStatus == ServiceApprovalStatus.Rejected && string.IsNullOrWhiteSpace(command.RejectionReason)) return BadRequest<WashServiceResultDto>("Trạng thái/lý do từ chối không hợp lệ.");

            var unchanged = entity.ApprovalStatus == command.ApprovalStatus && entity.IsActive == command.IsActive && entity.RejectionReason == command.RejectionReason;
            entity.ApprovalStatus = command.ApprovalStatus;
            entity.IsActive = command.IsActive;
            entity.RejectionReason = command.RejectionReason;

            await _serviceRepo.UpdateAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            Fill(dto, entity);
            var response = Success(dto, "Cập nhật trạng thái dịch vụ thành công!");
            if (unchanged) AdministrativeBatchExecutor.Unchanged(response);
            await Audit.LogAsync(new AuditEntry("Service", "Status", "WashService", entity.Id.ToString(), entity.Code));
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id!.Value, "Service", "Status", command.ServiceId.ToString(), command, response);
            await uow.CompleteAsync(); return response;
        }

        private async Task<string?> ApplyImagesAsync(WashService entity, List<string>? images)
        {
            if (images == null) return null;
            var existing = ReadImages(entity.Images);
            var added = images.Where(x => !existing.Contains(x?.Trim() ?? string.Empty)).ToList();
            var error = (await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), added, CurrentUser.Id!.Value, 0)).Error;
            if (error != null) return error;
            var clean = images.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList();
            if (clean.Count > UploadStorage.MaxImagesPerSubmission) return "Quá số ảnh cho phép.";
            entity.Images = clean.Count == 0 ? null : JsonSerializer.Serialize(clean); return null;
        }
        public Task<BaseResponse<AdministrativeBatchResultDto>> BatchAsync(AdministrativeBatchCommand command) =>
            LazyServiceProvider.LazyGetRequiredService<AdministrativeBatchExecutor>().RunAsync("Service", command, 50, async (item, key) =>
            {
                if (command.Action == "Delete") return AdministrativeBatchExecutor.Item(item.Id, await DeleteAsync(item.Id, item.ExpectedVersion, key));
                if (!command.IsActive.HasValue || !command.Status.HasValue || !Enum.IsDefined((ServiceApprovalStatus)command.Status.Value)) return new() { Id = item.Id, StatusCode = 400, Message = "Status cần isActive và approval status hợp lệ." };
                return AdministrativeBatchExecutor.Item(item.Id, await UpdateStatusAsync(new() { ServiceId = item.Id, ExpectedVersion = item.ExpectedVersion, OperationId = key, IsActive = command.IsActive.Value, ApprovalStatus = (ServiceApprovalStatus)command.Status.Value }));
            });
        private static List<string> ReadImages(string? json)
        { try { return json == null ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? []; } catch (JsonException) { return []; } }
        private static string Version(WashService x) => AdministrativeWriteGuard.Version(new { x.Id, x.Name, x.Code, x.Description, x.UnitPrice, x.PricePerKg, x.PricePerItem, x.Unit, x.ServiceType, x.EstimatedDurationHours, Images = ReadImages(x.Images), x.IsActive, x.ApprovalStatus, x.MerchantId, x.IsDeleted });
        private static void Fill(WashServiceResultDto dto, WashService entity) { dto.Version = Version(entity); dto.ImageUrls = ReadImages(entity.Images); }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, WashServiceFilterQuery query)
        {
            var result = await _serviceRepo.GetColumnDistinctValuesAsync(fieldName, query);
            return Success(result);
        }
    }
}
