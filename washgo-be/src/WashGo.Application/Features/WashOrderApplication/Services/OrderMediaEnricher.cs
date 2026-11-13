using System;
using System.Collections.Generic;
using System.Linq;
using WashGo.Application.Features.FileApplication.Services;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.Locker.Interfaces;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Entities.Partner.Interfaces;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using WashServiceEntity = global::WashGo.Domain.Entities.WashService.WashService;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>
    /// Ảnh và liên hệ cho danh sách/chi tiết đơn, nạp theo lô cho cả trang (không N+1): dịch vụ, trạm gửi/nhận,
    /// Merchant, avatar khách (chỉ Admin), liên hệ Shipper được giao (Admin/Merchant). Ảnh là ảnh hiện tại của tài nguyên.
    /// </summary>
    public class OrderMediaEnricher : ITransientDependency
    {
        private readonly IRepository<WashOrderItem, Guid> _itemRepo;
        private readonly IRepository<WashServiceEntity, Guid> _serviceRepo;
        private readonly ILockerRepository _lockerRepo;
        private readonly IRepository<Merchant, Guid> _merchantRepo;
        private readonly IRepository<MerchantImage, Guid> _merchantImageRepo;
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IShipperRepository _shipperRepo;
        private readonly IAsyncQueryableExecuter _executer;

        public OrderMediaEnricher(IRepository<WashOrderItem, Guid> itemRepo,
            IRepository<WashServiceEntity, Guid> serviceRepo, ILockerRepository lockerRepo,
            IRepository<Merchant, Guid> merchantRepo, IRepository<MerchantImage, Guid> merchantImageRepo,
            IRepository<User, Guid> userRepo, IShipperRepository shipperRepo, IAsyncQueryableExecuter executer)
        {
            _itemRepo = itemRepo;
            _serviceRepo = serviceRepo;
            _lockerRepo = lockerRepo;
            _merchantRepo = merchantRepo;
            _merchantImageRepo = merchantImageRepo;
            _userRepo = userRepo;
            _shipperRepo = shipperRepo;
            _executer = executer;
        }

        public async Task FillAsync<T>(List<T> orders, string role) where T : WashOrderResultDto
        {
            if (orders.Count == 0)
                return;
            var orderIds = orders.Select(x => x.Id).ToList();
            var items = await _executer.ToListAsync((await _itemRepo.GetQueryableAsync())
                .Where(x => orderIds.Contains(x.WashOrderId)).Select(x => new { x.WashOrderId, x.ServiceId, x.ServiceName }));
            var detailItems = orders.OfType<WashOrderDetailDto>().SelectMany(x => x.Items).ToList();
            var serviceIds = items.Select(x => x.ServiceId).Concat(detailItems.Select(x => x.ServiceId)).Distinct().ToList();
            var serviceImages = (await _executer.ToListAsync((await _serviceRepo.GetQueryableAsync())
                    .Where(x => serviceIds.Contains(x.Id)).Select(x => new { x.Id, x.Images })))
                .ToDictionary(x => x.Id, x => StoredImageUrls.Read(x.Images));

            var lockerIds = orders.SelectMany(x => new[] { x.LockerId, x.ReturnLockerId ?? x.LockerId }).Distinct().ToList();
            var lockerImages = (await _executer.ToListAsync((await _lockerRepo.GetQueryableAsync())
                    .Where(x => lockerIds.Contains(x.Id)).Select(x => new { x.Id, x.Images })))
                .ToDictionary(x => x.Id, x => StoredImageUrls.Read(x.Images));

            var merchantIds = orders.Where(x => x.MerchantId.HasValue).Select(x => x.MerchantId!.Value).Distinct().ToList();
            var merchantEmails = role == "Shipper"
                ? (await _executer.ToListAsync((await _userRepo.GetQueryableAsync()).Where(x => merchantIds.Contains(x.Id))
                    .Select(x => new { x.Id, x.Email }))).ToDictionary(x => x.Id, x => x.Email)
                : new Dictionary<Guid, string>();
            var merchantPhones = (await _executer.ToListAsync((await _merchantRepo.GetQueryableAsync())
                    .Where(x => merchantIds.Contains(x.Id)).Select(x => new { x.Id, x.Phone })))
                .ToDictionary(x => x.Id, x => x.Phone);
            var merchantImages = (await _executer.ToListAsync((await _merchantImageRepo.GetQueryableAsync())
                    .Where(x => merchantIds.Contains(x.MerchantId))
                    .Select(x => new { x.MerchantId, x.SecureUrl, x.IsPrimary, x.DisplayOrder })))
                .GroupBy(x => x.MerchantId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IsPrimary).ThenBy(x => x.DisplayOrder).Select(x => x.SecureUrl).ToList());

            var avatars = new Dictionary<Guid, string?>();
            if (role == "Admin")
            {
                var customerIds = orders.Select(x => x.CustomerId).Distinct().ToList();
                avatars = (await _executer.ToListAsync((await _userRepo.GetQueryableAsync())
                        .Where(x => customerIds.Contains(x.Id)).Select(x => new { x.Id, x.Avatar })))
                    .ToDictionary(x => x.Id, x => x.Avatar);
            }

            var shippers = new Dictionary<Guid, (string? Avatar, string? Phone, string Email, VehicleType Vehicle, string? Plate)>();
            if (role is "Admin" or "Merchant")
            {
                var shipperIds = orders.Where(x => x.ShipperId.HasValue).Select(x => x.ShipperId!.Value).Distinct().ToList();
                var rows = await _executer.ToListAsync(
                    from s in await _shipperRepo.GetQueryableAsync()
                    join u in await _userRepo.GetQueryableAsync() on s.UserId equals u.Id
                    where shipperIds.Contains(s.Id)
                    select new { s.Id, u.Avatar, u.Phone, u.Email, s.VehicleType, s.VehiclePlate });
                shippers = rows.ToDictionary(x => x.Id, x => (x.Avatar, x.Phone, x.Email, x.VehicleType, x.VehiclePlate));
            }

            foreach (var order in orders)
            {
                order.Services = items.Where(x => x.WashOrderId == order.Id)
                    .GroupBy(x => x.ServiceId)
                    .Select(g => new OrderServiceRefDto
                    {
                        ServiceId = g.Key, ServiceName = g.First().ServiceName,
                        ServiceThumbnailUrl = serviceImages.GetValueOrDefault(g.Key)?.FirstOrDefault()
                    }).ToList();
                order.LockerImageUrls = lockerImages.GetValueOrDefault(order.LockerId) ?? [];
                order.ReturnLockerImageUrls = lockerImages.GetValueOrDefault(order.ReturnLockerId ?? order.LockerId) ?? [];
                order.TaskLockerImageUrls = order.AssignedTaskType == DeliveryTaskType.Collect
                    ? order.LockerImageUrls : order.ReturnLockerImageUrls;
                if (order.MerchantId is { } merchantId)
                {
                    order.MerchantEmail = merchantEmails.GetValueOrDefault(merchantId);
                    order.MerchantPhoneNumber = merchantPhones.GetValueOrDefault(merchantId);
                    order.MerchantImageUrls = merchantImages.GetValueOrDefault(merchantId) ?? [];
                }
                if (role == "Admin")
                    order.CustomerAvatarUrl = avatars.GetValueOrDefault(order.CustomerId);
                if (order.ShipperId is { } shipperId && shippers.TryGetValue(shipperId, out var s))
                {
                    order.ShipperAvatarUrl = s.Avatar;
                    order.ShipperPhone = s.Phone;
                    order.ShipperEmail = s.Email;
                    order.ShipperVehicleType = s.Vehicle;
                    order.ShipperVehiclePlate = s.Plate;
                }
                if (order is WashOrderDetailDto detail)
                    foreach (var item in detail.Items)
                        item.ServiceImageUrls = serviceImages.GetValueOrDefault(item.ServiceId) ?? [];
            }
        }

    }
}
