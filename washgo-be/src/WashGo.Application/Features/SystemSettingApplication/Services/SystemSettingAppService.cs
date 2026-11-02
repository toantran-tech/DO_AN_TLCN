using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Commands;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Results;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.SystemConfig;

namespace WashGo.Application.Features.SystemSettingApplication.Services
{
    /// <summary>
    /// UC 3.9 Cài đặt hệ thống. Thay đổi áp dụng ngay cho các phép tính sau thời điểm lưu
    /// (hạn gửi đồ, SLA, phí quá hạn, phí hủy, bồi thường, hoa hồng mặc định của Merchant mới).
    /// </summary>
    public class SystemSettingAppService : WashGoAppService, ISystemSettingAppService
    {
        private const string InvalidMessage = "Thông số không hợp lệ.";
        private const decimal MaxFee = 10_000_000m;

        private readonly IRepository<SystemSetting, Guid> _repo;

        public SystemSettingAppService(IRepository<SystemSetting, Guid> repo)
        {
            _repo = repo;
        }

        public async Task<BaseResponse<SystemSettingDto>> GetAsync() =>
            Success(ToDto(await _repo.FindAsync(SystemSetting.SingletonId) ?? new SystemSetting()));

        public async Task<BaseResponse<SystemSettingDto>> UpdateSlaAsync(UpdateSlaSettingsCommand command)
        {
            if (command.DepositWindowMinutes is < 5 or > 1440 ||
                command.MerchantCollectSlaHours is < 1 or > 168 ||
                command.MerchantReturnSlaHours is < 1 or > 720 ||
                command.FreePickupHours is < 1 or > 720)
                return BadRequest<SystemSettingDto>(InvalidMessage);

            return await SaveAsync(s =>
            {
                s.DepositWindowMinutes = command.DepositWindowMinutes;
                s.MerchantCollectSlaHours = command.MerchantCollectSlaHours;
                s.MerchantReturnSlaHours = command.MerchantReturnSlaHours;
                s.FreePickupHours = command.FreePickupHours;
            }, "Cấu hình SLA thành công!");
        }

        public async Task<BaseResponse<SystemSettingDto>> UpdateCommissionAsync(UpdateCommissionSettingsCommand command)
        {
            if (command.DefaultCommissionRate is < 0 or > 100 ||
                decimal.Round(command.DefaultCommissionRate, 2) != command.DefaultCommissionRate)
                return BadRequest<SystemSettingDto>("Tỷ lệ phí phải từ 0-100%.");

            return await SaveAsync(s => s.DefaultCommissionRate = command.DefaultCommissionRate,
                "Cấu hình phí hoa hồng thành công!");
        }

        public async Task<BaseResponse<SystemSettingDto>> UpdatePenaltyAsync(UpdatePenaltySettingsCommand c)
        {
            static bool Fee(decimal value) => value is >= 0 and <= MaxFee && decimal.Round(value, 0) == value;
            static bool Percent(int value, int max) => value >= 0 && value <= max;
            if (!Fee(c.OverdueFeeDay1) || !Fee(c.OverdueFeeDay2) || !Fee(c.OverdueFeeDay3) || !Fee(c.StorageFeePerDay) ||
                !Percent(c.CancelLatePendingPercent, 100) || !Percent(c.CancelDepositedPercent, 100) ||
                !Percent(c.CancelCollectedPercent, 100) ||
                !Percent(c.CompensationLightPercent, 500) || !Percent(c.CompensationMediumPercent, 500) ||
                !Percent(c.CompensationSeverePercent, 500) || !Percent(c.CompensationLostPercent, 500) ||
                // Heavier damage never pays less than lighter damage.
                c.CompensationLightPercent > c.CompensationMediumPercent ||
                c.CompensationMediumPercent > c.CompensationSeverePercent ||
                c.CompensationSeverePercent > c.CompensationLostPercent)
                return BadRequest<SystemSettingDto>(InvalidMessage);

            return await SaveAsync(s =>
            {
                s.OverdueFeeDay1 = c.OverdueFeeDay1;
                s.OverdueFeeDay2 = c.OverdueFeeDay2;
                s.OverdueFeeDay3 = c.OverdueFeeDay3;
                s.StorageFeePerDay = c.StorageFeePerDay;
                s.CancelLatePendingPercent = c.CancelLatePendingPercent;
                s.CancelDepositedPercent = c.CancelDepositedPercent;
                s.CancelCollectedPercent = c.CancelCollectedPercent;
                s.CompensationLightPercent = c.CompensationLightPercent;
                s.CompensationMediumPercent = c.CompensationMediumPercent;
                s.CompensationSeverePercent = c.CompensationSeverePercent;
                s.CompensationLostPercent = c.CompensationLostPercent;
            }, "Cấu hình thành công!");
        }

        private async Task<BaseResponse<SystemSettingDto>> SaveAsync(Action<SystemSetting> apply, string message)
        {
            var setting = await _repo.FindAsync(SystemSetting.SingletonId);
            var isNew = setting == null;
            setting ??= new SystemSetting();
            apply(setting);
            setting.UpdatedBy = CurrentUser.Id;
            setting.UpdatedAt = DateTime.UtcNow;
            if (isNew)
                await _repo.InsertAsync(setting, autoSave: true);
            else
                await _repo.UpdateAsync(setting, autoSave: true);
            return Success(ToDto(setting), message);
        }

        private static SystemSettingDto ToDto(SystemSetting s) => new()
        {
            DepositWindowMinutes = s.DepositWindowMinutes,
            MerchantCollectSlaHours = s.MerchantCollectSlaHours,
            MerchantReturnSlaHours = s.MerchantReturnSlaHours,
            FreePickupHours = s.FreePickupHours,
            DefaultCommissionRate = s.DefaultCommissionRate,
            OverdueFeeDay1 = s.OverdueFeeDay1,
            OverdueFeeDay2 = s.OverdueFeeDay2,
            OverdueFeeDay3 = s.OverdueFeeDay3,
            StorageFeePerDay = s.StorageFeePerDay,
            StorageTransferAfterHours = SystemSetting.StorageTransferAfterHours,
            CancelLatePendingPercent = s.CancelLatePendingPercent,
            CancelDepositedPercent = s.CancelDepositedPercent,
            CancelCollectedPercent = s.CancelCollectedPercent,
            CompensationLightPercent = s.CompensationLightPercent,
            CompensationMediumPercent = s.CompensationMediumPercent,
            CompensationSeverePercent = s.CompensationSeverePercent,
            CompensationLostPercent = s.CompensationLostPercent,
            UpdatedAt = s.UpdatedAt
        };
    }
}
