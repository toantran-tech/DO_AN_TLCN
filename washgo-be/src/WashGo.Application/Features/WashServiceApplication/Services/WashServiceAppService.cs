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

namespace WashGo.Application.Features.WashServiceApplication.Services
{
    public class WashServiceAppService : WashGoAppService, IWashServiceAppService
    {
        private readonly IWashServiceRepository _serviceRepo;

        public WashServiceAppService(IWashServiceRepository serviceRepo)
        {
            _serviceRepo = serviceRepo;
        }

        public async Task<PaginatedResult<WashServiceResultDto>> GetListAsync(WashServiceFilterQuery query)
        {
            var (data, total) = await _serviceRepo.FilterProjectedDataAsync<WashServiceResultDto>(
                filterFunc: q =>
                {
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

            return Success(data, total, query.Page, query.Take);
        }

        public async Task<BaseResponse<WashServiceResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await _serviceRepo.FindAsync(id);
            if (entity == null)
            {
                return NotFound<WashServiceResultDto>("Không tìm thấy dịch vụ!");
            }

            var dto = ObjectMapper.Map<WashService, WashServiceResultDto>(entity);
            return Success(dto);
        }

        private async Task<string?> ValidateAsync(CreateWashServiceCommand command, Guid? currentId)
        {
            if (string.IsNullOrWhiteSpace(command.Code) || string.IsNullOrWhiteSpace(command.Name))
                return "Mã và tên dịch vụ không được để trống.";
            if (command.UnitPrice <= 0 || command.PricePerKg is <= 0 || command.PricePerItem is <= 0)
                return "Giá dịch vụ không hợp lệ.";
            if (command.EstimatedDurationHours is < 1 or > 8760)
                return "Thời gian xử lý phải từ 1 đến 8760 giờ.";
            if (await _serviceRepo.AnyAsync(x => x.Code == command.Code && x.Id != currentId))
                return $"Mã dịch vụ '{command.Code}' đã tồn tại.";
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
            return Created(dto, "Thêm mới dịch vụ thành công!");
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
            return Success(dto, "Cập nhật dịch vụ thành công!");
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
