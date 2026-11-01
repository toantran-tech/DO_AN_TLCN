using System;
using System.Threading.Tasks;
using WashGo.Core.Domain.DataAccess.Repository;

namespace WashGo.Domain.Entities.Feedback.Interfaces
{
    public interface IIncidentRepository : IMesRepository<Incident, Guid>
    {
        /// <summary>Đọc khiếu nại kèm khóa dòng để hai Admin không xử lý cùng lúc.</summary>
        Task<Incident?> GetForUpdateAsync(Guid id);
    }
}
