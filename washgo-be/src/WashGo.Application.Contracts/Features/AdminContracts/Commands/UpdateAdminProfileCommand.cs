namespace WashGo.Application.Contracts.Features.AdminContracts.Commands
{
    /// <summary>
    /// Admin tự sửa hồ sơ (chỉ chính mình, theo JWT). null = giữ nguyên; avatar "" = xóa ảnh;
    /// avatar mới phải là ảnh chính Admin upload qua /files/images. Không nhận userId/role/status/email.
    /// </summary>
    public class UpdateAdminProfileCommand
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Avatar { get; set; }
        /// <summary>Bắt buộc: version lấy từ GET /admin/profile.</summary>
        [System.Text.Json.Serialization.JsonConverter(typeof(WashGo.Core.Domain.Shared.Bases.OpaqueVersionConverter))]
        public string? ExpectedVersion { get; set; }
    }
}
