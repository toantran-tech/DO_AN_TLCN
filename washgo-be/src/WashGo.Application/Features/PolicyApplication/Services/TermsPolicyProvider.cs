using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.Policy;

namespace WashGo.Application.Features.PolicyApplication.Services
{
    /// <summary>Điều khoản hiện hành và trạng thái đồng ý; dùng chung cho API điều khoản và kết quả đăng nhập.</summary>
    public class TermsPolicyProvider : ITransientDependency
    {
        private readonly IRepository<TermsPolicy, Guid> _policies;
        private readonly IRepository<TermsAcceptance, Guid> _acceptances;

        public TermsPolicyProvider(IRepository<TermsPolicy, Guid> policies, IRepository<TermsAcceptance, Guid> acceptances)
        {
            _policies = policies;
            _acceptances = acceptances;
        }

        public async Task<TermsPolicy?> CurrentAsync()
        {
            var now = DateTime.UtcNow;
            var query = (await _policies.GetQueryableAsync())
                .Where(x => x.EffectiveFrom <= now)
                .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt);
            return await _policies.AsyncExecuter.FirstOrDefaultAsync(query);
        }

        public async Task<TermsAcceptance?> AcceptanceAsync(Guid userId, Guid policyId) =>
            await _acceptances.FirstOrDefaultAsync(x => x.UserId == userId && x.PolicyId == policyId);

        public async Task<TermsAcceptance?> LatestAcceptanceAsync(Guid userId)
        {
            var query = (await _acceptances.GetQueryableAsync())
                .Where(x => x.UserId == userId).OrderByDescending(x => x.AcceptedAt);
            return await _acceptances.AsyncExecuter.FirstOrDefaultAsync(query);
        }

        /// <summary>Customer phải đồng ý khi chưa đồng ý phiên bản hiện hành (lần đăng nhập đầu hoặc có phiên bản mới).</summary>
        public async Task<(bool Required, string? Version)> RequirementAsync(Guid userId, string roleName)
        {
            if (!string.Equals(roleName, "Customer", StringComparison.OrdinalIgnoreCase))
                return (false, null);
            var current = await CurrentAsync();
            if (current == null)
                return (false, null);
            return (await AcceptanceAsync(userId, current.Id) == null, current.Version);
        }
    }
}
