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
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.LockerApplication.Services
{
    public class LockerAppService : WashGoAppService, ILockerAppService
    {
        private readonly ILockerRepository _lockerRepo;

        public LockerAppService(ILockerRepository lockerRepo)
        {
            _lockerRepo = lockerRepo;
        }

        public async Task<PaginatedResult<LockerResultDto>> GetListAsync(LockerFilterQuery query)
        {
            var (data, total) = await _lockerRepo.FilterProjectedDataAsync<LockerResultDto>(
                filterFunc: q =>
                {
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

            return Success(data, total, query.Page, query.Take);
        }

        public async Task<BaseResponse<LockerResultDto>> GetByIdAsync(Guid id)
        {
            var locker = await _lockerRepo.GetWithBoxesAsync(id);
            if (locker == null)
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
            var locker = await _lockerRepo.GetWithBoxesAsync(command.Id);
            if (locker == null)
            {
                return NotFound<LockerResultDto>("Không tìm thấy trạm Locker để cập nhật!");
            }

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

            await _lockerRepo.UpdateAsync(locker, autoSave: true);
            var dto = ObjectMapper.Map<Locker, LockerResultDto>(locker);
            return Success(dto, "Cập nhật thông tin trạm Locker thành công!");
        }

        public async Task<BaseResponse<bool>> UpdateBoxStatusAsync(UpdateLockerBoxStatusCommand command)
        {
            var locker = await _lockerRepo.GetWithBoxesAsync(command.LockerId);
            if (locker == null)
            {
                return NotFound<bool>("Không tìm thấy tủ Locker!");
            }

            var box = locker.Boxes.FirstOrDefault(b => b.Id == command.BoxId);
            if (box == null)
            {
                return NotFound<bool>("Không tìm thấy ô tủ!");
            }

            box.Status = command.NewStatus;
            if (command.NewStatus == LockerBoxStatus.Available)
            {
                box.CurrentPinCode = null;
            }

            locker.AvailableBoxes = locker.Boxes.Count(b => b.Status == LockerBoxStatus.Available);

            await _lockerRepo.UpdateAsync(locker, autoSave: true);
            return Success(true, "Cập nhật trạng thái ô tủ thành công!");
        }

        public async Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, LockerFilterQuery query)
        {
            var result = await _lockerRepo.GetColumnDistinctValuesAsync(fieldName, query);
            return Success(result);
        }
    }
}
