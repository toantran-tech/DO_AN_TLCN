using WashGo.Application.Features.AuditApplication.Services;
using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using System.Net;
using System.Text.Json;
using WashGo.Application.Contracts.Features.AuditContracts.Results;
using WashGo.Application.Contracts.Features.AuditContracts.Queries;
using WashGo.Application.Contracts.Features.AuditContracts.Services;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Commands;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Results;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.SystemConfig;
using System.Globalization;

namespace WashGo.Application.Features.SystemSettingApplication.Services
{
    /// <summary>
    /// UC 3.9 Cài đặt hệ thống. Thay đổi áp dụng ngay cho các phép tính sau thời điểm lưu
    /// (hạn gửi đồ, SLA, phí quá hạn, phí hủy, bồi thường, hoa hồng mặc định của Merchant mới).
    /// </summary>
    public class SystemSettingAppService : WashGoAppService, ISystemSettingAppService
    {
        private const decimal MaxFee = 10_000_000m;

        private readonly IRepository<SystemSetting, Guid> _repo;

        public SystemSettingAppService(IRepository<SystemSetting, Guid> repo)
        {
            _repo = repo;
        }

        public async Task<BaseResponse<SystemSettingDto>> GetAsync()
        {
            var stored = await _repo.FindAsync(SystemSetting.SingletonId);
            return Success(ToDto(stored ?? new SystemSetting(), stored != null));
        }

        public async Task<BaseResponse<SystemSettingDto>> UpdateSlaAsync(UpdateSlaSettingsCommand command)
        {
            if (command.DepositWindowMinutes is < 5 or > 1440 ||
                command.MerchantCollectSlaHours is < 1 or > 168 ||
                command.MerchantReturnSlaHours is < 1 or > 720 ||
                command.FreePickupHours is < 1 or > 720)
                return Invalid(command, "SLA phải nằm trong giới hạn số nguyên đã công bố.");

            return await SaveAsync(s =>
            {
                s.DepositWindowMinutes = command.DepositWindowMinutes;
                s.MerchantCollectSlaHours = command.MerchantCollectSlaHours;
                s.MerchantReturnSlaHours = command.MerchantReturnSlaHours;
                s.FreePickupHours = command.FreePickupHours;
            }, "Cấu hình SLA thành công!", command, "Sla");
        }

        public async Task<BaseResponse<SystemSettingDto>> UpdateCommissionAsync(UpdateCommissionSettingsCommand command)
        {
            if (command.DefaultCommissionRate is < 0 or > 100 ||
                decimal.Round(command.DefaultCommissionRate, 2) != command.DefaultCommissionRate)
                return Invalid(command, "Tỷ lệ phí phải từ 0–100%, tối đa hai chữ số thập phân.");

            return await SaveAsync(s => s.DefaultCommissionRate = command.DefaultCommissionRate,
                "Cấu hình phí hoa hồng thành công!", command, "Commission");
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
                return Invalid(c, "Phí/phần trăm không hợp lệ hoặc mức bồi thường không theo thứ tự nhẹ ≤ vừa ≤ nặng ≤ mất.");

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
            }, "Cấu hình thành công!", c, "Penalty");
        }

        private async Task<BaseResponse<SystemSettingDto>> SaveAsync(Action<SystemSetting> apply, string message, SettingWriteCommand command, string group)
        {
            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var payload = JsonSerializer.SerializeToElement(command, command.GetType());
            var resource = SystemSetting.SingletonId.ToString();
            if (await Writes.ReplayAsync<SystemSettingDto>(command.OperationId, CurrentUser.Id!.Value, "SystemSetting", group, resource, payload) is { } replay) return replay;
            await Writes.LockAsync("system-settings");
            var setting = await _repo.FindAsync(SystemSetting.SingletonId);
            var isNew = setting == null;
            setting ??= new SystemSetting();
            var auditBefore = ToDto(setting, !isNew);
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
                return AdministrativeWriteGuard.Error<SystemSettingDto>(HttpStatusCode.BadRequest, "ExpectedVersionRequired", "Thiếu expectedVersion.");
            if (command.ExpectedVersion != auditBefore.Version)
                return AdministrativeWriteGuard.Error(HttpStatusCode.Conflict, "VersionMismatch", "Cấu hình đã thay đổi; hãy tải lại.", auditBefore);
            apply(setting);
            setting.UpdatedBy = CurrentUser.Id;
            setting.UpdatedAt = new DateTime(Math.Max(DateTime.UtcNow.Ticks / 10, (setting.UpdatedAt?.Ticks ?? 0) / 10 + 1) * 10, DateTimeKind.Utc);
            if (isNew)
                await _repo.InsertAsync(setting, autoSave: true);
            else
                await _repo.UpdateAsync(setting, autoSave: true);
            var auditAfter = ToDto(setting);
            var changed = AuditLogWriter.Diff(auditBefore, auditAfter);
            await Audit.LogAsync(new AuditEntry("SystemSetting", "Update" + group, "SystemSetting", SystemSetting.SingletonId.ToString(), null)
            {
                ChangedFields = changed, Details = new { before = AuditLogWriter.Pick(auditBefore, changed), after = AuditLogWriter.Pick(auditAfter, changed) }
            });
            var response = Success(auditAfter, message);
            await Writes.RecordAsync(command.OperationId, CurrentUser.Id.Value, "SystemSetting", group, resource, payload, response);
            await uow.CompleteAsync();
            return response;
        }

        public async Task<BaseResponse<AdministrativeOperationDto>> GetOperationAsync(Guid id)
        {
            var operation = await Writes.FindAsync(id, CurrentUser.Id!.Value, "SystemSetting");
            return operation == null ? NotFound<AdministrativeOperationDto>("Chưa có kết quả đã commit cho key này; không chứng minh yêu cầu đang chạy đã thất bại.") :
                Success(new AdministrativeOperationDto { OperationId = operation.Id, Action = operation.Action, ResourceId = operation.ResourceId, CompletedAt = operation.CompletedAt, Result = JsonSerializer.Deserialize<JsonElement>(operation.ResponseJson) });
        }

        public Task<PaginatedResult<AuditLogDto>> GetHistoryAsync(AuditLogFilterQuery query)
        {
            query.Module = "SystemSetting"; query.ResourceType = "SystemSetting"; query.ResourceId = SystemSetting.SingletonId.ToString();
            return LazyServiceProvider.LazyGetRequiredService<IAuditLogAppService>().GetListAsync(query);
        }

        private static SystemSettingDto ToDto(SystemSetting s, bool persisted = true) => new()
        {
            Version = s.UpdatedAt.HasValue ? (s.UpdatedAt.Value.Ticks / 10).ToString(CultureInfo.InvariantCulture) : "defaults-v1",
            IsPersisted = persisted, ServerNow = DateTime.UtcNow, UpdatedBy = s.UpdatedBy,
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
        private static BaseResponse<SystemSettingDto> Invalid(SettingWriteCommand command, string message)
        {
            var response = AdministrativeWriteGuard.Error<SystemSettingDto>(HttpStatusCode.BadRequest, "InvalidSettings", message);
            foreach (var field in command.GetType().GetProperties().Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(decimal)))
            {
                var value = Convert.ToDecimal(field.GetValue(command));
                var (minimum, maximum, decimals) = field.Name switch
                {
                    "DepositWindowMinutes" => (5m, 1440m, 0), "MerchantCollectSlaHours" => (1m, 168m, 0),
                    "MerchantReturnSlaHours" or "FreePickupHours" => (1m, 720m, 0),
                    "DefaultCommissionRate" => (0m, 100m, 2),
                    _ when field.Name.StartsWith("Compensation") => (0m, 500m, 0),
                    _ when field.Name.StartsWith("Cancel") => (0m, 100m, 0),
                    _ => (0m, MaxFee, 0)
                };
                if (value < minimum || value > maximum || decimal.Round(value, decimals) != value)
                    response.Errors[JsonNamingPolicy.CamelCase.ConvertName(field.Name)] = [$"Giá trị phải từ {minimum} đến {maximum}, tối đa {decimals} chữ số thập phân."];
            }
            if (command is UpdatePenaltySettingsCommand p && (p.CompensationLightPercent > p.CompensationMediumPercent || p.CompensationMediumPercent > p.CompensationSeverePercent || p.CompensationSeverePercent > p.CompensationLostPercent))
                response.Errors["compensationPercentOrder"] = ["Mức bồi thường phải theo thứ tự nhẹ ≤ vừa ≤ nặng ≤ mất."];
            return response;
        }
    }
}
