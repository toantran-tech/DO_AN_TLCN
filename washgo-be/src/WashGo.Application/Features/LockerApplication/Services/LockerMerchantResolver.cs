using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using WashGo.Domain.Entities.Locker;
using WashGo.Domain.Entities.Partner;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.LockerApplication.Services
{
    /// <summary>Merchant phụ trách locker (nhiều Merchant / locker), dùng cho tạo đơn, phân công đơn (UC 3.3) và trả đồ.</summary>
    public class LockerMerchantResolver : ITransientDependency
    {
        private readonly IRepository<LockerMerchant, Guid> _links;
        private readonly IRepository<Merchant, Guid> _merchants;
        private readonly IRepository<Locker, Guid> _lockers;
        private readonly IAsyncQueryableExecuter _executer;

        public LockerMerchantResolver(IRepository<LockerMerchant, Guid> links, IRepository<Merchant, Guid> merchants,
            IRepository<Locker, Guid> lockers, IAsyncQueryableExecuter executer)
        {
            _links = links;
            _merchants = merchants;
            _lockers = lockers;
            _executer = executer;
        }

        /// <summary>
        /// Merchant đang hoạt động của locker và locker nằm trong bán kính phục vụ của Merchant, theo độ ưu tiên (1 trước).
        /// Liên kết cũ ngoài bán kính/thiếu tọa độ được giữ nhưng không nhận đơn mới (catalog, tạo đơn, phân công dùng cùng quy tắc).
        /// </summary>
        public async Task<List<LockerMerchant>> ActiveLinksAsync(Guid lockerId)
        {
            var locker = await _lockers.FindAsync(lockerId);
            if (locker == null) return [];
            var links = await _links.GetQueryableAsync();
            var merchants = await _merchants.GetQueryableAsync();
            var rows = await _executer.ToListAsync(
                from l in links
                join m in merchants on l.MerchantId equals m.Id
                where l.LockerId == lockerId && m.Status == MerchantStatus.Active
                orderby l.Priority
                select new { Link = l, m.Latitude, m.Longitude, m.ServiceRadiusKm });
            return rows.Where(x => MerchantCoverage.Covers(x.Latitude, x.Longitude, x.ServiceRadiusKm, locker.Latitude, locker.Longitude))
                .Select(x => x.Link).ToList();
        }

        public async Task<bool> HasActiveMerchantAsync(Guid lockerId) => (await ActiveLinksAsync(lockerId)).Count > 0;

        public Task<bool> ServesAsync(Guid lockerId, Guid? merchantId) =>
            merchantId.HasValue
                ? _links.AnyAsync(l => l.LockerId == lockerId && l.MerchantId == merchantId.Value)
                : Task.FromResult(false);
    }
}
