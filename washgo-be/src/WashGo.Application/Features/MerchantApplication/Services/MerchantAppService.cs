using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;
using WashGo.Application.Contracts.Features.MerchantContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.MerchantApplication.Services
{
    public class MerchantAppService : WashGoAppService, IMerchantAppService
    {
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IRepository<MerchantServiceArea, Guid> _msaRepo;

        public MerchantAppService(
            IRepository<Merchant, Guid> merchantRepo,
            IRepository<User, Guid> userRepo,
            IRepository<MerchantServiceArea, Guid> msaRepo)
        {
            _merchantRepo = merchantRepo;
            _userRepo = userRepo;
            _msaRepo = msaRepo;
        }

        public async Task<PaginatedResult<MerchantResultDto>> GetListAsync(MerchantFilterQuery query)
        {
            var queryable = await _merchantRepo.GetQueryableAsync();

            if (query.Status.HasValue)
                queryable = queryable.Where(x => x.Status == query.Status.Value);

            if (!string.IsNullOrWhiteSpace(query.City))
                queryable = queryable.Where(x => x.City == query.City);

            if (!string.IsNullOrWhiteSpace(query.District))
                queryable = queryable.Where(x => x.District == query.District);

            if (!string.IsNullOrWhiteSpace(query.Keyword))
            {
                var kw = query.Keyword.Trim().ToLower();
                queryable = queryable.Where(x => x.BusinessName.ToLower().Contains(kw) ||
                                                 x.Address.ToLower().Contains(kw) ||
                                                 x.Phone.Contains(kw));
            }

            var total = await AsyncExecuter.CountAsync(queryable);
            var items = await AsyncExecuter.ToListAsync(
                queryable.OrderBy(x => x.BusinessName)
                         .Skip((query.Page - 1) * query.Take)
                         .Take(query.Take)
            );

            var dtos = ObjectMapper.Map<List<Merchant>, List<MerchantResultDto>>(items);

            // Populate ServiceAreaIds for each DTO
            foreach (var dto in dtos)
            {
                var msaList = await _msaRepo.GetListAsync(x => x.MerchantId == dto.Id && x.IsActive);
                dto.ServiceAreaIds = msaList.Select(x => x.ServiceAreaId).ToList();
            }

            return Success(dtos, total, query.Page, query.Take);
        }

        public async Task<BaseResponse<MerchantResultDto>> GetByIdAsync(Guid id)
        {
            var entity = await _merchantRepo.FindAsync(id);
            if (entity == null)
            {
                return NotFound<MerchantResultDto>("Không tìm thấy đối tác giặt sấy!");
            }

            var dto = ObjectMapper.Map<Merchant, MerchantResultDto>(entity);
            var msaList = await _msaRepo.GetListAsync(x => x.MerchantId == id && x.IsActive);
            dto.ServiceAreaIds = msaList.Select(x => x.ServiceAreaId).ToList();
            return Success(dto);
        }

        public async Task<BaseResponse<MerchantResultDto>> CreateAsync(CreateMerchantCommand command)
        {
            var user = await _userRepo.FindAsync(command.UserId);
            if (user == null)
            {
                return BadRequest<MerchantResultDto>("Tài khoản User được chỉ định không tồn tại.");
            }

            var existingMerchant = await _merchantRepo.FindAsync(command.UserId);
            if (existingMerchant != null)
            {
                return BadRequest<MerchantResultDto>("Hồ sơ Merchant cho User này đã tồn tại.");
            }

            var entity = ObjectMapper.Map<CreateMerchantCommand, Merchant>(command);
            entity.Status = MerchantStatus.Active;
            entity.Rating = 5.0m;
            entity.CreatedAt = DateTime.UtcNow;
            entity.UpdatedAt = DateTime.UtcNow;

            await _merchantRepo.InsertAsync(entity, autoSave: true);

            if (command.ServiceAreaIds != null && command.ServiceAreaIds.Any())
            {
                foreach (var areaId in command.ServiceAreaIds)
                {
                    await _msaRepo.InsertAsync(new MerchantServiceArea
                    {
                        MerchantId = entity.Id,
                        ServiceAreaId = areaId,
                        Priority = 1,
                        IsActive = true,
                        AssignedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }, autoSave: true);
                }
            }

            var dto = ObjectMapper.Map<Merchant, MerchantResultDto>(entity);
            dto.ServiceAreaIds = command.ServiceAreaIds ?? [];
            return Created(dto, "Tạo hồ sơ đối tác giặt sấy thành công!");
        }

        public async Task<BaseResponse<MerchantResultDto>> UpdateAsync(UpdateMerchantCommand command)
        {
            var entity = await _merchantRepo.FindAsync(command.Id);
            if (entity == null)
            {
                return NotFound<MerchantResultDto>("Không tìm thấy hồ sơ đối tác giặt sấy!");
            }

            ObjectMapper.Map(command, entity);
            entity.UpdatedAt = DateTime.UtcNow;

            await _merchantRepo.UpdateAsync(entity, autoSave: true);

            if (command.ServiceAreaIds != null)
            {
                var existingMsa = await _msaRepo.GetListAsync(x => x.MerchantId == entity.Id);
                foreach (var msa in existingMsa)
                {
                    await _msaRepo.DeleteAsync(msa);
                }

                foreach (var areaId in command.ServiceAreaIds)
                {
                    await _msaRepo.InsertAsync(new MerchantServiceArea
                    {
                        MerchantId = entity.Id,
                        ServiceAreaId = areaId,
                        Priority = 1,
                        IsActive = true,
                        AssignedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    }, autoSave: true);
                }
            }

            var dto = ObjectMapper.Map<Merchant, MerchantResultDto>(entity);
            dto.ServiceAreaIds = command.ServiceAreaIds ?? [];
            return Success(dto, "Cập nhật hồ sơ Merchant thành công!");
        }

        public async Task<BaseResponse<MerchantResultDto>> UpdateStatusAsync(UpdateMerchantStatusCommand command)
        {
            var entity = await _merchantRepo.FindAsync(command.MerchantId);
            if (entity == null)
            {
                return NotFound<MerchantResultDto>("Không tìm thấy đối tác Merchant!");
            }

            entity.Status = command.Status;
            if (command.Status == MerchantStatus.Active)
            {
                entity.ApprovedBy = command.ApprovedBy;
                entity.ApprovedAt = DateTime.UtcNow;
            }
            else if (command.Status == MerchantStatus.Inactive || command.Status == MerchantStatus.Suspended)
            {
                entity.RejectionReason = command.RejectionReason;
            }
            entity.UpdatedAt = DateTime.UtcNow;

            await _merchantRepo.UpdateAsync(entity, autoSave: true);
            var dto = ObjectMapper.Map<Merchant, MerchantResultDto>(entity);
            return Success(dto, $"Đã cập nhật trạng thái Merchant sang {command.Status}.");
        }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, MerchantFilterQuery query)
        {
            var queryable = await _merchantRepo.GetQueryableAsync();
            var values = await AsyncExecuter.ToListAsync(queryable.Select(x => x.City).Distinct());
            var result = values.Where(v => !string.IsNullOrEmpty(v))
                               .Select(v => new ColumnFilterDistinctValueDto { Value = v, Label = v, Count = 1 })
                               .ToList();
            return Success(result);
        }
    }
}
