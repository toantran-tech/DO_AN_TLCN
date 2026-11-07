using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.PolicyContracts.Commands;
using WashGo.Application.Contracts.Features.PolicyContracts.Results;
using WashGo.Application.Contracts.Features.PolicyContracts.Services;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Policy;
using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.Application.Features.PolicyApplication.Services
{
    /// <summary>
    /// Điều khoản dịch vụ cho Customer: xem phiên bản hiện hành kèm quy định đang áp dụng (hạn gửi, phí hủy,
    /// phí quá hạn…) và ghi nhận đồng ý theo phiên bản trên server, để mọi thiết bị biết khách đã xem.
    /// </summary>
    public class PolicyAppService : WashGoAppService, IPolicyAppService
    {
        private const int MaxContentLength = 50_000;

        private readonly TermsPolicyProvider _provider;
        private readonly IRepository<TermsPolicy, Guid> _policies;
        private readonly IRepository<TermsAcceptance, Guid> _acceptances;
        private readonly SystemSettingProvider _settings;

        public PolicyAppService(TermsPolicyProvider provider, IRepository<TermsPolicy, Guid> policies,
            IRepository<TermsAcceptance, Guid> acceptances, SystemSettingProvider settings)
        {
            _provider = provider;
            _policies = policies;
            _acceptances = acceptances;
            _settings = settings;
        }

        public async Task<BaseResponse<CurrentPolicyDto>> GetCurrentAsync()
        {
            var current = await _provider.CurrentAsync();
            if (current == null)
                return NotFound<CurrentPolicyDto>("Chưa có điều khoản dịch vụ đang áp dụng.");
            var result = new CurrentPolicyDto();
            await FillAsync(result, current);
            return Success(result);
        }

        public async Task<BaseResponse<CustomerPolicyDto>> GetForCustomerAsync()
        {
            var current = await _provider.CurrentAsync();
            if (current == null)
                return NotFound<CustomerPolicyDto>("Chưa có điều khoản dịch vụ đang áp dụng.");
            return Success(await CustomerViewAsync(current));
        }

        public async Task<BaseResponse<CustomerPolicyDto>> AcknowledgeAsync(AcknowledgeTermsCommand command)
        {
            var current = await _provider.CurrentAsync();
            if (current == null)
                return NotFound<CustomerPolicyDto>("Chưa có điều khoản dịch vụ đang áp dụng.");
            if (!string.Equals(command.Version?.Trim(), current.Version, StringComparison.Ordinal))
                return BadRequest<CustomerPolicyDto>("Điều khoản đã có phiên bản mới. Vui lòng tải lại và xem lại điều khoản.");

            var userId = CurrentUser.Id!.Value;
            // A replay (another device, double click) keeps the first acceptance; the unique index guards races.
            if (await _provider.AcceptanceAsync(userId, current.Id) == null)
                await _acceptances.InsertAsync(new TermsAcceptance
                {
                    UserId = userId,
                    PolicyId = current.Id,
                    Version = current.Version,
                    AcceptedAt = DateTime.UtcNow,
                    IpAddress = Truncate(command.IpAddress, 64),
                    UserAgent = Truncate(command.UserAgent, 500)
                }, autoSave: true);
            return Success(await CustomerViewAsync(current), "Đã ghi nhận bạn đồng ý điều khoản dịch vụ.");
        }

        public async Task<BaseResponse<List<TermsPolicyDto>>> GetVersionsAsync()
        {
            var list = (await _policies.GetListAsync()).OrderByDescending(x => x.EffectiveFrom).Select(ToDto).ToList();
            return Success(list);
        }

        public async Task<BaseResponse<TermsPolicyDto>> PublishAsync(PublishTermsCommand command)
        {
            var version = command.Version?.Trim() ?? "";
            var title = command.Title?.Trim() ?? "";
            var content = command.Content?.Trim() ?? "";
            if (version.Length is 0 or > 20 || title.Length is 0 or > 200 || content.Length is 0 or > MaxContentLength)
                return BadRequest<TermsPolicyDto>("Vui lòng nhập phiên bản (tối đa 20 ký tự), tiêu đề (tối đa 200 ký tự) và nội dung điều khoản.");
            if (await _policies.AnyAsync(x => x.Version == version))
                return BadRequest<TermsPolicyDto>("Phiên bản điều khoản này đã tồn tại.");

            var effectiveFrom = command.EffectiveFrom.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(command.EffectiveFrom.Value).UtcDateTime
                : DateTime.UtcNow;
            var policy = new TermsPolicy
            {
                Version = version,
                Title = title,
                Content = content,
                EffectiveFrom = effectiveFrom,
                CreatedBy = CurrentUser.Id,
                CreatedAt = DateTime.UtcNow
            };
            await _policies.InsertAsync(policy, autoSave: true);
            return Success(ToDto(policy), "Đã phát hành điều khoản mới. Customer sẽ được yêu cầu đồng ý lại khi điều khoản có hiệu lực.");
        }

        private async Task<CustomerPolicyDto> CustomerViewAsync(TermsPolicy current)
        {
            var userId = CurrentUser.Id!.Value;
            var accepted = await _provider.AcceptanceAsync(userId, current.Id);
            var latest = accepted ?? await _provider.LatestAcceptanceAsync(userId);
            var result = new CustomerPolicyDto
            {
                Accepted = accepted != null,
                AcceptedAt = accepted?.AcceptedAt,
                AcceptedVersion = latest?.Version
            };
            await FillAsync(result, current);
            return result;
        }

        private async Task FillAsync(CurrentPolicyDto result, TermsPolicy current)
        {
            var s = await _settings.GetAsync();
            result.Terms = ToDto(current);
            result.Rules = new PolicyRulesDto
            {
                DepositWindowMinutes = s.DepositWindowMinutes,
                CancelLatePendingPercent = s.CancelLatePendingPercent,
                CancelDepositedPercent = s.CancelDepositedPercent,
                CancelCollectedPercent = s.CancelCollectedPercent,
                FreePickupHours = s.FreePickupHours,
                OverdueFeeDay1 = s.OverdueFeeDay1,
                OverdueFeeDay2 = s.OverdueFeeDay2,
                OverdueFeeDay3 = s.OverdueFeeDay3,
                StorageFeePerDay = s.StorageFeePerDay,
                StorageTransferAfterHours = SystemSetting.StorageTransferAfterHours,
                UnpaidStorageAfterDays = UnpaidOrderStorageMover.StorageAfterDays,
                CompensationLightPercent = s.CompensationLightPercent,
                CompensationMediumPercent = s.CompensationMediumPercent,
                CompensationSeverePercent = s.CompensationSeverePercent,
                CompensationLostPercent = s.CompensationLostPercent,
                SettingsUpdatedAt = s.UpdatedAt
            };
            result.ServerNow = DateTime.UtcNow;
        }

        private static TermsPolicyDto ToDto(TermsPolicy p) => new()
        {
            Id = p.Id,
            Version = p.Version,
            Title = p.Title,
            Content = p.Content,
            EffectiveFrom = p.EffectiveFrom,
            CreatedAt = p.CreatedAt
        };

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
    }
}
