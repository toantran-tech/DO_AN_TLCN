using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
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
        public async Task<BaseResponse<bool>> DeleteAsync(Guid id)
        {
            var entity = await _serviceRepo.FindAsync(id);
            if (entity == null || entity.IsDeleted)
                return NotFound<bool>("Không tìm thấy dịch vụ!");
            var items = await _itemRepo.GetQueryableAsync();
            var orders = await _orderRepo.GetQueryableAsync();
            var inUse = await AsyncExecuter.AnyAsync(
                from item in items
                join order in orders on item.WashOrderId equals order.Id
                where item.ServiceId == id && !order.IsDeleted &&
                      order.Status != WashOrderStatus.Completed && order.Status != WashOrderStatus.Cancelled
                select item.Id);
            if (inUse)
                return BadRequest<bool>("Không thể xóa dịch vụ đang có đơn hàng.");

            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.DeletedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            entity.DeletedBy = CurrentUser.Id?.ToString();
            await _serviceRepo.UpdateAsync(entity, autoSave: true);
            return Success(true, "Xóa Dịch vụ thành công!");
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
                defaultOrder: q => q.OrderBy(x => x.Code)
            );

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
            return Success(dto);
        }

        private async Task<string?> ValidateAsync(CreateWashServiceCommand command, Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(command.Code))
                return "Mã dịch vụ không được để trống.";
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
            var error = await ValidateAsync(command, null);
            if (error != null)
                return BadRequest<WashServiceResultDto>(error);

            var entity = ObjectMapper.Map<CreateWashServiceCommand, WashService>(command);
            entity.ApprovalStatus = Domain.Shared.Enums.ServiceApprovalStatus.Approved;
            entity.IsActive = true;
            await _serviceRepo.InsertAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            return Created(dto, "Thêm Dịch vụ thành công!");
        }

        public async Task<BaseResponse<WashServiceResultDto>> UpdateAsync(UpdateWashServiceCommand command)
        {
            var entity = await _serviceRepo.FindAsync(command.Id);
            if (entity == null)
            {
                return NotFound<WashServiceResultDto>("Không tìm thấy dịch vụ để cập nhật!");
            }

            var error = await ValidateAsync(command, command.Id);
            if (error != null)
                return BadRequest<WashServiceResultDto>(error);

            ObjectMapper.Map(command, entity);
            await _serviceRepo.UpdateAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            return Success(dto, "Cập nhật Dịch vụ thành công!");
        }

        public async Task<BaseResponse<WashServiceResultDto>> UpdateStatusAsync(UpdateWashServiceStatusCommand command)
        {
            var entity = await _serviceRepo.FindAsync(command.ServiceId);
            if (entity == null)
            {
                return NotFound<WashServiceResultDto>("Không tìm thấy dịch vụ!");
            }

            entity.ApprovalStatus = command.ApprovalStatus;
            entity.IsActive = command.IsActive;
            entity.RejectionReason = command.RejectionReason;

            await _serviceRepo.UpdateAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            return Success(dto, "Cập nhật trạng thái dịch vụ thành công!");
        }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, WashServiceFilterQuery query)
        {
            var result = await _serviceRepo.GetColumnDistinctValuesAsync(fieldName, query);
            return Success(result);
        }
    }
}
