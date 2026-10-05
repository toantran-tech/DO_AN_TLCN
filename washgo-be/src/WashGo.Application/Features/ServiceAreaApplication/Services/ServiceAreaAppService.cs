using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Results;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Partner;

namespace WashGo.Application.Features.ServiceAreaApplication.Services
{
    public class ServiceAreaAppService : WashGoAppService, IServiceAreaAppService
    {
        private readonly IRepository<ServiceArea, Guid> _serviceAreaRepo;

        public ServiceAreaAppService(IRepository<ServiceArea, Guid> serviceAreaRepo)
        {
            _serviceAreaRepo = serviceAreaRepo;
        }

        public async Task<PaginatedResult<ServiceAreaResultDto>> GetListAsync(ServiceAreaFilterQuery query)
        {
            var queryable = await _serviceAreaRepo.GetQueryableAsync();

            if (query.IsActive.HasValue)
                queryable = queryable.Where(x => x.IsActive == query.IsActive.Value);

            if (!string.IsNullOrWhiteSpace(query.City))
                queryable = queryable.Where(x => x.City == query.City);

            if (!string.IsNullOrWhiteSpace(query.District))
                queryable = queryable.Where(x => x.District == query.District);

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.Code.ToLower().Contains(kw) ||
                                                 x.Name.ToLower().Contains(kw) ||
                                                 x.District.ToLower().Contains(kw));
            }

            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(x => x.Code)
                         .Skip((query.Page - 1) * query.Take)
                         .Take(query.Take)
            );

            var dtos = ObjectMapper.Map<List<ServiceArea>, List<ServiceAreaResultDto>>(items);
            return Success(dtos, total, query.Page, query.Take);
        }

        public async Task<BaseResponse<ServiceAreaResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await _serviceAreaRepo.FindAsync(id);
            if (entity == null)
            {
                return NotFound<ServiceAreaResultDto>("Không tìm thấy Vùng phục vụ!");
            }

            var dto = ObjectMapper.Map<ServiceArea, ServiceAreaResultDto>(entity);
            return Success(dto);
        }

        public async Task<BaseResponse<ServiceAreaResultDto>> CreateAsync(CreateServiceAreaCommand command)
        {
            var existingCode = await _serviceAreaRepo.FirstOrDefaultAsync(x => x.Code == command.Code);
            if (existingCode != null)
            {
                return BadRequest<ServiceAreaResultDto>($"Mã vùng phục vụ '{command.Code}' đã tồn tại.");
            }

            var entity = ObjectMapper.Map<CreateServiceAreaCommand, ServiceArea>(command);
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            await _serviceAreaRepo.InsertAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<ServiceArea, ServiceAreaResultDto>(entity);
            return Created(dto, "Tạo Vùng phục vụ mới thành công!");
        }

        public async Task<BaseResponse<ServiceAreaResultDto>> UpdateAsync(UpdateServiceAreaCommand command)
        {
            var entity = await _serviceAreaRepo.FindAsync(command.Id);
            if (entity == null)
            {
                return NotFound<ServiceAreaResultDto>("Không tìm thấy Vùng phục vụ để cập nhật!");
            }

            ObjectMapper.Map(command, entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _serviceAreaRepo.UpdateAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<ServiceArea, ServiceAreaResultDto>(entity);
            return Success(dto, "Cập nhật Vùng phục vụ thành công!");
        }

        public async Task<BaseResponse<bool>> ToggleActiveAsync(Guid id)
        {
            var entity = await _serviceAreaRepo.FindAsync(id);
            if (entity == null)
            {
                return NotFound<bool>("Không tìm thấy Vùng phục vụ!");
            }

            entity.IsActive = !entity.IsActive;
            entity.UpdatedAt = DateTime.UtcNow;

            await _serviceAreaRepo.UpdateAsync(entity, autoSave: true);
            return Success(entity.IsActive, $"Đã {(entity.IsActive ? "kích hoạt" : "vô hiệu hóa")} vùng phục vụ.");
        }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, ServiceAreaFilterQuery query)
        {
            var queryable = await _serviceAreaRepo.GetQueryableAsync();
            var values = await AsyncExecuter.ToListAsync(queryable.Select(x => x.City).Distinct());
            var result = values.Where(v => !string.IsNullOrEmpty(v))
                               .Select(v => new ColumnFilterDistinctValueDto { Value = v, Label = v, Count = 1 })
                               .ToList();
            return Success(result);
        }
    }
}
