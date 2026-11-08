using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;
using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Commands
{
    /// <summary>UC 3.2.2: Admin tạo tài khoản (role Merchant, mật khẩu tạm gửi qua email) và hồ sơ Merchant.</summary>
    public class CreateMerchantCommand
    {
        /// <summary>Key riêng của tạo Merchant; giữ nguyên key/payload khi gửi lại. Không áp dụng khi sửa.</summary>
        [JsonIgnore(Condition =JsonIgnoreCondition.WhenWritingNull)]
        public Guid? ClientRequestId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        /// <summary>Giấy phép kinh doanh (số giấy phép hoặc đường dẫn tệp đã upload).</summary>
        public string? BusinessLicense { get; set; }
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        /// <summary>Ảnh cửa hàng đã upload qua /files/images; khi sửa, null = giữ nguyên.</summary>
        public List<string>? ImageUrls { get; set; }
        public decimal DepositAmount { get; set; } = 1000000;
        /// <summary>Để trống khi tạo mới thì dùng % hoa hồng mặc định (UC 3.9.2); để trống khi sửa thì giữ nguyên.</summary>
        public decimal? CommissionRate { get; set; }
        public int ServiceRadiusKm { get; set; } = 5;
        public int MaxCapacity { get; set; } = 50;
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public string? SlaDocUrl { get; set; }
        /// <summary>Khi sửa: null = giữ nguyên vùng phục vụ (thiết lập chi tiết ở UC 3.2.5).</summary>
        public List<Guid>? ServiceAreaIds { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? BusinessLicenseDocumentId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? ContractDocumentId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? SlaDocumentId { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RemoveBusinessLicenseDocument { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RemoveContractDocument { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool RemoveSlaDocument { get; set; }
    }

    /// <summary>UC 3.2.3: Admin chỉnh sửa thông tin Merchant, SLA, chiết khấu.</summary>
    public class UpdateMerchantCommand : CreateMerchantCommand
    {
        public Guid Id { get; set; }
        public string? ExpectedVersion { get; set; }
        public Guid? OperationId { get; set; }
        /// <summary>Chỉ chấp nhận giá trị hiện tại để tương thích client cũ. Đổi trạng thái dùng PUT /merchant/status.</summary>
        public MerchantStatus? Status { get; set; }
    }

    /// <summary>UC 3.2.4: khóa (Suspended/Inactive, bắt buộc lý do) hoặc mở khóa (Active).</summary>
    public class UpdateMerchantStatusCommand
    {
        public string? ExpectedVersion { get; set; }
        public Guid? OperationId { get; set; }
        public Guid MerchantId { get; set; }
        public MerchantStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        /// <summary>Không còn dùng: người duyệt lấy từ tài khoản Admin đang đăng nhập.</summary>
        public Guid? ApprovedBy { get; set; }
        /// <summary>Xác nhận vẫn khóa khi Merchant còn đơn chưa xử lý (UC 3.2.4 6a).</summary>
        public bool Confirm { get; set; }
    }
}
