using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.Application.Features.SystemSettingApplication.Services
{
    /// <summary>Đọc cấu hình hiện hành; nếu bản ghi chưa có (DB cũ) thì dùng giá trị mặc định.</summary>
    public class SystemSettingProvider : ITransientDependency
    {
        private readonly IRepository<SystemSetting, System.Guid> _repo;

        public SystemSettingProvider(IRepository<SystemSetting, System.Guid> repo)
        {
            _repo = repo;
        }

        public async Task<SystemSetting> GetAsync() =>
            await _repo.FindAsync(SystemSetting.SingletonId) ?? new SystemSetting();
    }
}
