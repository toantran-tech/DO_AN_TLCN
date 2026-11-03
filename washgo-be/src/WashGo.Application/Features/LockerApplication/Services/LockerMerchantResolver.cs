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
        private readonly IAsyncQueryableExecuter _executer;

        public LockerMerchantResolver(IRepository<LockerMerchant, Guid> links, IRepository<Merchant, Guid> merchants,
            IAsyncQueryableExecuter executer)
        {
            _links = links;
            _merchants = merchants;
            _executer = executer;
        }

        /// <summary>Merchant đang hoạt động của locker, theo độ ưu tiên (1 trước).</summary>
        public async Task<List<LockerMerchant>> ActiveLinksAsync(Guid lockerId)
        {
            var links = await _links.GetQueryableAsync();
            var merchants = await _merchants.GetQueryableAsync();
            return await _executer.ToListAsync(links
                .Where(l => l.LockerId == lockerId &&
                            merchants.Any(m => m.Id == l.MerchantId && m.Status == MerchantStatus.Active))
                .OrderBy(l => l.Priority));
        }

        public async Task<bool> HasActiveMerchantAsync(Guid lockerId) => (await ActiveLinksAsync(lockerId)).Count > 0;

        public Task<bool> ServesAsync(Guid lockerId, Guid? merchantId) =>
            merchantId.HasValue
                ? _links.AnyAsync(l => l.LockerId == lockerId && l.MerchantId == merchantId.Value)
                : Task.FromResult(false);
    }
}
