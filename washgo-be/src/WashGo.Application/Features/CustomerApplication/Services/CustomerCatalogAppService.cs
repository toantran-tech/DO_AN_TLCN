using Volo.Abp.Domain.Repositories;
using System.Linq.Expressions;
using WashGo.Application.Contracts.Features.CustomerContracts.Queries;
using WashGo.Application.Contracts.Features.CustomerContracts.Results;
using WashGo.Application.Contracts.Features.CustomerContracts.Services;
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
    private readonly IRepository<LockerMerchant, Guid> _lockerMerchants;

    public CustomerCatalogAppService(
        IRepository<WashService, Guid> services,
        IRepository<Locker, Guid> lockers,
        IRepository<LockerBox, Guid> slots,
        IRepository<Merchant, Guid> merchants,
        IRepository<LockerMerchant, Guid> lockerMerchants)
    {
        _services = services;
        _lockers = lockers;
        _slots = slots;
        _merchants = merchants;
        _lockerMerchants = lockerMerchants;
    }

    // Shared catalog: one price per service for every Merchant (Admin approves Merchant proposals).
    public async Task<PaginatedResult<CustomerServiceCatalogDto>> GetServicesAsync(CustomerServiceFilterQuery filter)
    {
        var sorts = filter.SortBy ?? [];
        var serviceTypes = (filter.ServiceTypes ?? []).Distinct().ToArray();
        var ranges = filter.PriceRanges ?? [];
        string? error = sorts.Any(sort => sort == null ||
            sort.Field?.Trim().ToLowerInvariant() is not ("name" or "unitprice" or "price"))
            ? "SortBy.Field chỉ chấp nhận Name hoặc UnitPrice (price)."
            : serviceTypes.Any(type => !Enum.IsDefined(type))
                ? "ServiceTypes chứa loại dịch vụ không hợp lệ."
                : ranges.Any(range => range == null ||
                    (!range.MinPrice.HasValue && !range.MaxPrice.HasValue) ||
                    range.MinPrice is < 0 || range.MaxPrice is < 0 ||
                    (range.MinPrice.HasValue && range.MaxPrice.HasValue && range.MinPrice.Value > range.MaxPrice.Value))
                    ? "Mỗi khoảng giá phải có ít nhất một đầu mút, giá không âm và giá tối thiểu không lớn hơn giá tối đa."
                    : null;
        if (error != null)
            return new PaginatedResult<CustomerServiceCatalogDto>
            {
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Succeeded = false,
                Message = error
            };

        var (page, take) = NormalizePaging(filter.Page, filter.Take);
        var services = await _services.GetQueryableAsync();
        var query = services.Where(service => !service.IsDeleted && service.IsActive
            && service.ApprovalStatus == ServiceApprovalStatus.Approved && service.UnitPrice > 0);

        if (serviceTypes.Length > 0)
            query = query.Where(x => serviceTypes.Contains(x.ServiceType));
        if (ranges.Length > 0)
        {
            // Build one SQL predicate: OR within price ranges, AND with other criteria.
            // Avoid compiling the predicate or filtering in memory before paging.
            var service = Expression.Parameter(typeof(WashService), "service");
            var price = Expression.Property(service, nameof(WashService.UnitPrice));
            Expression anyRange = Expression.Constant(false);
            foreach (var range in ranges)
            {
                Expression match = Expression.Constant(true);
                if (range.MinPrice.HasValue)
                    match = Expression.AndAlso(match, Expression.GreaterThanOrEqual(price, Expression.Constant(range.MinPrice.Value)));
                if (range.MaxPrice.HasValue)
                    match = Expression.AndAlso(match, Expression.LessThanOrEqual(price, Expression.Constant(range.MaxPrice.Value)));
                anyRange = Expression.OrElse(anyRange, match);
            }
            query = query.Where(Expression.Lambda<Func<WashService, bool>>(anyRange, service));
        }
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var term = filter.Keyword.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Code.ToLower().Contains(term));
        }

        var total = await AsyncExecuter.CountAsync(query);
        IOrderedQueryable<WashService>? ordered = null;
        foreach (var sort in sorts)
        {
            if (sort.Field.Trim().Equals("name", StringComparison.OrdinalIgnoreCase))
                ordered = ordered == null
                    ? (sort.IsDescending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name))
                    : (sort.IsDescending ? ordered.ThenByDescending(x => x.Name) : ordered.ThenBy(x => x.Name));
            else
                ordered = ordered == null
                    ? (sort.IsDescending ? query.OrderByDescending(x => x.UnitPrice) : query.OrderBy(x => x.UnitPrice))
                    : (sort.IsDescending ? ordered.ThenByDescending(x => x.UnitPrice) : ordered.ThenBy(x => x.UnitPrice));
        }
        ordered ??= query.OrderBy(x => x.Name);
        var list = await AsyncExecuter.ToListAsync(ordered.ThenBy(x => x.Id)
            .Skip((page - 1) * take).Take(take)
            .Select(x => new CustomerServiceCatalogDto
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                ServiceType = x.ServiceType,
                Unit = x.Unit,
                UnitPrice = x.UnitPrice,
                PricePerKg = x.PricePerKg,
                PricePerItem = x.PricePerItem,
                EstimatedDurationHours = x.EstimatedDurationHours,
                Images = x.Images
            }));
        return Success(list, total, page, take);
    }

    public async Task<BaseResponse<CustomerServiceCatalogDto>> GetServiceAsync(Guid id)
    {
        var services = await _services.GetQueryableAsync();
        var query = from service in services
                    where service.Id == id && !service.IsDeleted && service.IsActive
                        && service.ApprovalStatus == ServiceApprovalStatus.Approved && service.UnitPrice > 0
                    select new CustomerServiceCatalogDto
                    {
                        Id = service.Id,
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
        string? city, string? district, string? keyword, int page, int take,
        double? latitude = null, double? longitude = null)
    {
        (page, take) = NormalizePaging(page, take);
        // UC 1.2.2: with the customer's position, show the distance and list the nearest lockers first.
        var hasPosition = latitude.HasValue && longitude.HasValue;
        if (hasPosition && (latitude is < -90 or > 90 || longitude is < -180 or > 180))
            return new PaginatedResult<CustomerLockerCatalogDto>
            {
                Succeeded = false,
                StatusCode = System.Net.HttpStatusCode.BadRequest,
                Message = "Vị trí không hợp lệ."
            };
        var lat = latitude ?? 0;
        var lng = longitude ?? 0;
        var lockers = await _lockers.GetQueryableAsync();
        var slots = await _slots.GetQueryableAsync();
        var merchants = await _merchants.GetQueryableAsync();
        var links = await _lockerMerchants.GetQueryableAsync();
        // A locker is open to customers while at least one of its Merchants is active.
        var query = from locker in lockers
                    where !locker.IsDeleted && locker.Status == LockerStatus.Active
                        && links.Any(l => l.LockerId == locker.Id
                            && merchants.Any(m => m.Id == l.MerchantId && m.Status == MerchantStatus.Active))
                        && slots.Any(slot => slot.LockerId == locker.Id && !slot.IsDeleted
                            && slot.Status == LockerBoxStatus.Available)
                    select new { locker };

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

        const double Rad = Math.PI / 180;
        var withDistance = query.Select(x => new
        {
            x.locker,
            // Haversine, translated to SQL so sorting and paging stay on the server.
            distance = 2 * 6371 * Math.Asin(Math.Sqrt(
                Math.Pow(Math.Sin((x.locker.Latitude - lat) * Rad / 2), 2) +
                Math.Cos(lat * Rad) * Math.Cos(x.locker.Latitude * Rad) *
                Math.Pow(Math.Sin((x.locker.Longitude - lng) * Rad / 2), 2)))
        });
        var ordered = hasPosition
            ? withDistance.OrderBy(x => x.distance).ThenBy(x => x.locker.Id)
            : withDistance.OrderBy(x => x.locker.Name).ThenBy(x => x.locker.Id);

        var total = await AsyncExecuter.CountAsync(query);
        var list = await AsyncExecuter.ToListAsync(ordered
            .Skip((page - 1) * take).Take(take)
            .Select(x => new CustomerLockerCatalogDto
            {
                Id = x.locker.Id,
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
                Images = x.locker.Images,
                DistanceKm = hasPosition ? Math.Round(x.distance, 2) : null
            }));
        // UC 1.2.1 4a.
        return Success(list, total, page, take, total == 0 ? "Hiện tại không có locker khả dụng." : null);
    }

    public async Task<BaseResponse<CustomerLockerCatalogDto>> GetLockerAsync(Guid id)
    {
        var lockers = await _lockers.GetQueryableAsync();
        var merchants = await _merchants.GetQueryableAsync();
        var links = await _lockerMerchants.GetQueryableAsync();
        var query = from locker in lockers
                    where locker.Id == id && !locker.IsDeleted && locker.Status == LockerStatus.Active
                        && links.Any(l => l.LockerId == locker.Id
                            && merchants.Any(m => m.Id == l.MerchantId && m.Status == MerchantStatus.Active))
                    select new CustomerLockerCatalogDto
                    {
                        Id = locker.Id,
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
