using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Results;
using WashGo.Application.Contracts.Features.ServiceAreaContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;
using WashGo.Domain.Entities.Feedback;

namespace WashGo.Application.Features.ServiceAreaApplication.Services
{
    public class ServiceAreaAppService : WashGoAppService, IServiceAreaAppService
    {
        private readonly IRepository<ServiceArea, Guid> _serviceAreaRepo;
        private readonly IRepository<MerchantServiceArea, Guid> _msaRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;

        public ServiceAreaAppService(
            IRepository<ServiceArea, Guid> serviceAreaRepo,
            IRepository<MerchantServiceArea, Guid> msaRepo,
            IRepository<Merchant, Guid> merchantRepo)
        {
            _serviceAreaRepo = serviceAreaRepo;
            _msaRepo = msaRepo;
            _merchantRepo = merchantRepo;
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
            await FillMerchantCountsAsync(dtos);
            return Success(dtos, total, query.Page, query.Take, total == 0 ? "Chưa có vùng phục vụ nào." : null);
        }

        public async Task<BaseResponse<ServiceAreaResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await _serviceAreaRepo.FindAsync(id);
            if (entity == null)
            {
                return NotFound<ServiceAreaResultDto>("Không tìm thấy Vùng phục vụ!");
            }

            var dto = ObjectMapper.Map<ServiceArea, ServiceAreaResultDto>(entity);
            await FillMerchantCountsAsync([dto]);
            return Success(dto);
        }

        // UC 3.2.5 steps 3-6: active Merchants with their current assignment to this area.
        public async Task<BaseResponse<AreaMerchantsDto>> GetMerchantsAsync(Guid id)
        {
            var area = await _serviceAreaRepo.FindAsync(id);
            if (area == null)
                return NotFound<AreaMerchantsDto>("Không tìm thấy Vùng phục vụ!");
            return Success(await AreaMerchantsAsync(area));
        }

        // UC 3.2.5 steps 7-9: replaces the Merchant list of the area.
        public async Task<BaseResponse<AreaMerchantsDto>> SetMerchantsAsync(Guid id, SetAreaMerchantsCommand command)
        {
            var area = await _serviceAreaRepo.FindAsync(id);
            if (area == null)
                return NotFound<AreaMerchantsDto>("Không tìm thấy Vùng phục vụ!");
            var items = command.Merchants ?? [];
            if (items.Count == 0 && !command.ConfirmEmpty)
                return new BaseResponse<AreaMerchantsDto>
                {
                    Succeeded = false,
                    StatusCode = HttpStatusCode.Conflict,
                    Data = await AreaMerchantsAsync(area),
                    Message = "Vùng này chưa có Merchant nào phục vụ. Bạn có chắc muốn lưu?"
                };
            if (items.Select(x => x.MerchantId).Distinct().Count() != items.Count)
                return BadRequest<AreaMerchantsDto>("Mỗi Merchant chỉ được chọn một lần.");
            if (items.Any(x => x.Priority is < 1 or > 100))
                return BadRequest<AreaMerchantsDto>("Độ ưu tiên phải từ 1 đến 100 (1 là cao nhất).");
            var ids = items.Select(x => x.MerchantId).ToList();
            var active = await AsyncExecuter.CountAsync((await _merchantRepo.GetQueryableAsync())
                .Where(x => ids.Contains(x.Id) && x.Status == MerchantStatus.Active));
            if (active != ids.Count)
                return BadRequest<AreaMerchantsDto>("Chỉ được chọn Merchant đang hoạt động.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var now = DateTime.UtcNow;
            var existing = await _msaRepo.GetListAsync(x => x.ServiceAreaId == id);
            await _msaRepo.DeleteManyAsync(existing, autoSave: true);
            await _msaRepo.InsertManyAsync(items.Select(x => new MerchantServiceArea(GuidGenerator.Create())
            {
                MerchantId = x.MerchantId,
                ServiceAreaId = id,
                Priority = x.Priority,
                IsActive = true,
                AssignedBy = CurrentUser.Id,
                AssignedAt = now,
                CreatedAt = now,
                UpdatedAt = now
            }), autoSave: true);
            await uow.CompleteAsync();
            return Success(await AreaMerchantsAsync(area), "Thiết lập vùng phục vụ thành công!");
        }

        private async Task<AreaMerchantsDto> AreaMerchantsAsync(ServiceArea area)
        {
            var links = (await _msaRepo.GetListAsync(x => x.ServiceAreaId == area.Id && x.IsActive))
                .GroupBy(x => x.MerchantId).ToDictionary(g => g.Key, g => g.Min(x => x.Priority));
            var linkedIds = links.Keys.ToList();
            // Active Merchants plus any inactive Merchant still linked, so the Admin can see and remove it.
            var merchants = await _merchantRepo.GetListAsync(x => x.Status == MerchantStatus.Active || linkedIds.Contains(x.Id));
            var merchantIds = merchants.Select(m => m.Id).ToList();
            var ratingCounts = (await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<OrderRating, Guid>>()
                    .GetQueryableAsync()).Where(r => !r.IsDeleted && !r.IsHidden && r.MerchantId != null && merchantIds.Contains(r.MerchantId.Value))
                    .GroupBy(r => r.MerchantId!.Value).Select(g => new { g.Key, Count = g.Count() })))
                .ToDictionary(x => x.Key, x => x.Count);
            var dto = ObjectMapper.Map<ServiceArea, ServiceAreaResultDto>(area);
            dto.MerchantCount = links.Count;
            return new AreaMerchantsDto
            {
                Area = dto,
                Merchants = merchants
                    .Select(m => new AreaMerchantDto
                    {
                        MerchantId = m.Id,
                        BusinessName = m.BusinessName,
                        District = m.District,
                        City = m.City,
                        Rating = ratingCounts.GetValueOrDefault(m.Id) == 0 ? null : m.Rating,
                        PublicRatingCount = ratingCounts.GetValueOrDefault(m.Id),
                        Assigned = links.ContainsKey(m.Id),
                        Priority = links.TryGetValue(m.Id, out var p) ? p : null
                    })
                    .OrderBy(x => x.Assigned ? 0 : 1).ThenBy(x => x.Priority ?? int.MaxValue).ThenBy(x => x.BusinessName)
                    .ToList()
            };
        }

        private async Task FillMerchantCountsAsync(List<ServiceAreaResultDto> areas)
        {
            var ids = areas.Select(x => x.Id).ToList();
            var counts = (await _msaRepo.GetListAsync(x => ids.Contains(x.ServiceAreaId) && x.IsActive))
                .GroupBy(x => x.ServiceAreaId).ToDictionary(g => g.Key, g => g.Select(x => x.MerchantId).Distinct().Count());
            foreach (var dto in areas)
                dto.MerchantCount = counts.GetValueOrDefault(dto.Id);
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
