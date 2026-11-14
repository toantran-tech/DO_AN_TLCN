using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.AdminContracts.Commands;
using WashGo.Application.Contracts.Features.AdminContracts.Results;
using WashGo.Application.Contracts.Features.AdminContracts.Services;
using WashGo.Application.Features.AuditApplication.Services;
using WashGo.Application.Features.FileApplication.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.Identity;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Validation;
using System.Collections.Generic;
using System.Globalization;

namespace WashGo.Application.Features.AdminApplication.Services
{
    public class AdminProfileAppService : WashGoAppService, IAdminProfileAppService
    {
        private readonly IRepository<User, Guid> _userRepo;
        private readonly IConfiguration _configuration;

        public AdminProfileAppService(IRepository<User, Guid> userRepo, IConfiguration configuration)
        {
            _userRepo = userRepo;
            _configuration = configuration;
        }

        public async Task<BaseResponse<AdminProfileDto>> GetAsync()
        {
            var user = CurrentUser.Id.HasValue ? await _userRepo.FindAsync(CurrentUser.Id.Value) : null;
            return user == null ? NotFound<AdminProfileDto>("Không tìm thấy tài khoản.") : Success(ToDto(user));
        }

        public async Task<BaseResponse<AdminProfileDto>> UpdateAsync(UpdateAdminProfileCommand command)
        {
            if (!CurrentUser.Id.HasValue)
                return NotFound<AdminProfileDto>("Không tìm thấy tài khoản.");
            if (string.IsNullOrWhiteSpace(command.ExpectedVersion))
                return BadRequest<AdminProfileDto>("Thiếu expectedVersion (lấy từ GET /admin/profile).");
            var fullName = command.FullName?.Trim();
            if (fullName != null && (fullName.Length < 2 || fullName.Length > 100))
                return BadRequest<AdminProfileDto>("Họ tên phải có từ 2 đến 100 ký tự.");
            var phone = command.Phone?.Trim();
            if (!string.IsNullOrEmpty(phone) && !CustomerPhoneRules.IsValid(phone))
                return BadRequest<AdminProfileDto>("Số điện thoại không hợp lệ.");
            var avatar = command.Avatar?.Trim();
            if (!string.IsNullOrEmpty(avatar) &&
                (await UploadStorage.ValidateOwnedImagesAsync(LazyServiceProvider.LazyGetRequiredService<ObjectStorage>(), [avatar], CurrentUser.Id.Value, 1)).Error != null)
                return BadRequest<AdminProfileDto>("Ảnh đại diện phải là ảnh do chính bạn upload qua /files/images.");

            using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            // Serialise saves of the same profile so the version check and the write cannot interleave.
            await LazyServiceProvider.LazyGetRequiredService<IWashOrderRepository>().AcquireLockAsync($"admin-profile:{CurrentUser.Id.Value}");
            var user = await _userRepo.FindAsync(CurrentUser.Id.Value);
            if (user == null)
                return NotFound<AdminProfileDto>("Không tìm thấy tài khoản.");
            if (Version(user) != command.ExpectedVersion.Trim())
                return new BaseResponse<AdminProfileDto>
                {
                    Succeeded = false, StatusCode = HttpStatusCode.Conflict, Data = ToDto(user),
                    Message = "Hồ sơ đã được thay đổi ở nơi khác; dữ liệu mới nhất được trả kèm, vui lòng kiểm tra rồi lưu lại."
                };

            var changed = new List<string>();
            if (fullName != null && fullName != user.FullName) { user.FullName = fullName; changed.Add("fullName"); }
            if (phone != null)
            {
                var value = phone.Length == 0 ? null : phone;
                if (value != user.Phone) { user.Phone = value; changed.Add("phone"); }
            }
            if (avatar != null)
            {
                var value = avatar.Length == 0 ? null : avatar;
                if (value != user.Avatar) { user.Avatar = value; changed.Add("avatar"); }
            }
            if (changed.Count == 0)
            {
                await uow.CompleteAsync();
                return Success(ToDto(user), "Không có thay đổi nào.");
            }
            var now = DateTime.UtcNow;
            user.UpdatedAt = new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc); // PostgreSQL keeps microseconds
            await _userRepo.UpdateAsync(user, autoSave: true);
            await Audit.LogAsync(new AuditEntry("AdminProfile", "Update", "User", user.Id.ToString(), user.Email) { ChangedFields = changed });
            await uow.CompleteAsync();
            return Success(ToDto(user), "Đã cập nhật hồ sơ.");
        }

        // UpdatedAt has microsecond precision in PostgreSQL: its ticks / 10 make a stable version.
        private static string Version(User user) => (user.UpdatedAt.Ticks / 10).ToString(CultureInfo.InvariantCulture);

        private static AdminProfileDto ToDto(User user) => new()
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Avatar = user.Avatar,
            JoinedAt = DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc),
            AccountStatus = user.IsActive ? "Active" : "Locked",
            LastLoginAt = user.LastLoginAt.HasValue ? DateTime.SpecifyKind(user.LastLoginAt.Value, DateTimeKind.Utc) : null,
            Version = Version(user)
        };
    }
}
