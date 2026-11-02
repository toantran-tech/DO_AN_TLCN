using System;
using System.Threading.Tasks;
using WashGo.Core.Domain.DataAccess.Repository;

namespace WashGo.Domain.Entities.Promotion.Interfaces
{
    public interface IPromotionRepository : IMesRepository<Promotion, Guid>
    {
        /// <summary>Tăng lượt dùng bằng một UPDATE có điều kiện; giữ khóa dòng đến hết transaction.</summary>
        Task<bool> TryConsumeAsync(Guid promotionId, DateTime now);
        Task<bool> TryReleaseAsync(Guid promotionId);
    }
}
