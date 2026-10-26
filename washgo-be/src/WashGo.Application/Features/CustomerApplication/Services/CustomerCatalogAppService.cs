using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.CustomerContracts;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.CustomerApplication.Services;

public class CustomerCatalogAppService : WashGoAppService, ICustomerCatalogAppService
{
    private readonly IRepository<WashService, Guid> _services;
    private readonly IRepository<Locker, Guid> _lockers;
    private readonly IRepository<LockerBox, Guid> _slots;
    private readonly IRepository<Merchant, Guid> _merchants;

    public CustomerCatalogAppService(
        IRepository<WashService, Guid> services,
        IRepository<Locker, Guid> lockers,
        IRepository<LockerBox, Guid> slots,
        IRepository<Merchant, Guid> merchants)
    {
        _services = services;
        _lockers = lockers;
        _slots = slots;
        _merchants = merchants;
    }

    public async Task<PaginatedResult<CustomerServiceCatalogDto>> GetServicesAsync(
        Guid? merchantId, WashServiceType? serviceType, string? keyword, int page, int take)
    {
        (page, take) = NormalizePaging(page, take);
        var services = await _services.GetQueryableAsync();
        var merchants = await _merchants.GetQueryableAsync();
        var query = from service in services
                    join merchant in merchants on service.MerchantId equals merchant.Id
                    where !service.IsDeleted && service.IsActive
                        && service.ApprovalStatus == ServiceApprovalStatus.Approved
                        && merchant.Status == MerchantStatus.Active
                    select new { service, merchant };

        if (merchantId.HasValue)
            query = query.Where(x => x.service.MerchantId == merchantId.Value);
        if (serviceType.HasValue)
            query = query.Where(x => x.service.ServiceType == serviceType.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLower();
            query = query.Where(x => x.service.Name.ToLower().Contains(term) || x.service.Code.ToLower().Contains(term));
        }

        var total = await AsyncExecuter.CountAsync(query);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.service.Name).ThenBy(x => x.service.Id)
            .Skip((page - 1) * take).Take(take)
            .Select(x => new CustomerServiceCatalogDto
            {
                Id = x.service.Id,
                MerchantId = x.service.MerchantId,
                MerchantName = x.merchant.BusinessName,
                Code = x.service.Code,
                Name = x.service.Name,
                Description = x.service.Description,
                ServiceType = x.service.ServiceType,
                Unit = x.service.Unit,
                UnitPrice = x.service.UnitPrice,
                PricePerKg = x.service.PricePerKg,
                PricePerItem = x.service.PricePerItem,
                EstimatedDurationHours = x.service.EstimatedDurationHours,
                Images = x.service.Images
            }));
        return Success(list, total, page, take);
    }

    public async Task<BaseResponse<CustomerServiceCatalogDto>> GetServiceAsync(Guid id)
    {
        var services = await _services.GetQueryableAsync();
        var merchants = await _merchants.GetQueryableAsync();
        var query = from service in services
                    join merchant in merchants on service.MerchantId equals merchant.Id
                    where service.Id == id && !service.IsDeleted && service.IsActive
                        && service.ApprovalStatus == ServiceApprovalStatus.Approved
                        && merchant.Status == MerchantStatus.Active
                    select new CustomerServiceCatalogDto
                    {
                        Id = service.Id,
                        MerchantId = service.MerchantId,
                        MerchantName = merchant.BusinessName,
                        Code = service.Code,
                        Name = service.Name,
                        Description = service.Description,
                        ServiceType = service.ServiceType,
                        Unit = service.Unit,
                        UnitPrice = service.UnitPrice,
                        PricePerKg = service.PricePerKg,
                        PricePerItem = service.PricePerItem,
                        EstimatedDurationHours = service.EstimatedDurationHours,
                        Images = service.Images
                    };
        var dto = await AsyncExecuter.FirstOrDefaultAsync(query);
        return dto == null ? NotFound<CustomerServiceCatalogDto>("Không tìm thấy dịch vụ đang bán.") : Success(dto);
    }

    public async Task<PaginatedResult<CustomerLockerCatalogDto>> GetLockersAsync(
        string? city, string? district, string? keyword, int page, int take)
    {
        (page, take) = NormalizePaging(page, take);
        var lockers = await _lockers.GetQueryableAsync();
        var slots = await _slots.GetQueryableAsync();
        var merchants = await _merchants.GetQueryableAsync();
        var query = from locker in lockers
                    join merchant in merchants on locker.MerchantId equals merchant.Id
                    where !locker.IsDeleted && locker.Status == LockerStatus.Active
                        && merchant.Status == MerchantStatus.Active
                        && slots.Any(slot => slot.LockerId == locker.Id && !slot.IsDeleted
                            && slot.Status == LockerBoxStatus.Available)
                    select new { locker, merchant };

        if (!string.IsNullOrWhiteSpace(city))
        {
            var term = city.Trim().ToLower();
            query = query.Where(x => x.locker.City.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(district))
        {
            var term = district.Trim().ToLower();
            query = query.Where(x => x.locker.District.ToLower().Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim().ToLower();
            query = query.Where(x => x.locker.Name.ToLower().Contains(term)
                || x.locker.Code.ToLower().Contains(term)
                || x.locker.Address.ToLower().Contains(term));
        }

        var total = await AsyncExecuter.CountAsync(query);
        var list = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.locker.Name).ThenBy(x => x.locker.Id)
            .Skip((page - 1) * take).Take(take)
            .Select(x => new CustomerLockerCatalogDto
            {
                Id = x.locker.Id,
                MerchantId = x.locker.MerchantId,
                MerchantName = x.merchant.BusinessName,
                Code = x.locker.Code,
                Name = x.locker.Name,
                Address = x.locker.Address,
                Ward = x.locker.Ward,
                District = x.locker.District,
                City = x.locker.City,
                Latitude = x.locker.Latitude,
                Longitude = x.locker.Longitude,
                AvailableSlotCount = slots.Count(slot => slot.LockerId == x.locker.Id && !slot.IsDeleted
                    && slot.Status == LockerBoxStatus.Available),
                Description = x.locker.Description,
                Images = x.locker.Images
            }));
        return Success(list, total, page, take);
    }

    public async Task<BaseResponse<CustomerLockerCatalogDto>> GetLockerAsync(Guid id)
    {
        var lockers = await _lockers.GetQueryableAsync();
        var merchants = await _merchants.GetQueryableAsync();
        var query = from locker in lockers
                    join merchant in merchants on locker.MerchantId equals merchant.Id
                    where locker.Id == id && !locker.IsDeleted && locker.Status == LockerStatus.Active
                        && merchant.Status == MerchantStatus.Active
                    select new CustomerLockerCatalogDto
                    {
                        Id = locker.Id,
                        MerchantId = locker.MerchantId,
                        MerchantName = merchant.BusinessName,
                        Code = locker.Code,
                        Name = locker.Name,
                        Address = locker.Address,
                        Ward = locker.Ward,
                        District = locker.District,
                        City = locker.City,
                        Latitude = locker.Latitude,
                        Longitude = locker.Longitude,
                        Description = locker.Description,
                        Images = locker.Images
                    };
        var dto = await AsyncExecuter.FirstOrDefaultAsync(query);
        if (dto == null)
            return NotFound<CustomerLockerCatalogDto>("Không tìm thấy tủ đang hoạt động.");

        var slots = await _slots.GetQueryableAsync();
        dto.AvailableSlots = await AsyncExecuter.ToListAsync(slots
            .Where(slot => slot.LockerId == id && !slot.IsDeleted && slot.Status == LockerBoxStatus.Available)
            .OrderBy(slot => slot.SlotNumber)
            .Select(slot => new CustomerLockerSlotDto
            {
                Id = slot.Id,
                SlotNumber = slot.SlotNumber,
                Size = slot.Size
            }));
        if (dto.AvailableSlots.Count == 0)
            return NotFound<CustomerLockerCatalogDto>("Tủ hiện không còn ô trống.");
        dto.AvailableSlotCount = dto.AvailableSlots.Count;
        return Success(dto);
    }

    private static (int Page, int Take) NormalizePaging(int page, int take) =>
        (Math.Clamp(page, 1, 100_000), Math.Clamp(take, 1, 100));
}
