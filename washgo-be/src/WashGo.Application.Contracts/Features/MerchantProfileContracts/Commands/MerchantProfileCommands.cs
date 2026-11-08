using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.MerchantProfileContracts.Commands
{
    /// <summary>Các trường Merchant được tự sửa (UC 2.1.2); ImageUrls = null giữ nguyên ảnh hiện tại.</summary>
    public class MerchantProfileUpdateCommand
    {
        public string BusinessName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public List<string>? ImageUrls { get; set; }
        /// <summary>Mã số thuế (10 số hoặc 10 số-3 số): null = giữ nguyên, chuỗi rỗng = xóa. Chỉ áp dụng khi Admin duyệt.</summary>
        public string? TaxCode { get; set; }
        /// <summary>
        /// Giấy phép kinh doanh: số giấy phép (tối đa 100 ký tự) hoặc ảnh giấy phép đã upload qua /files/images (phải do
        /// chính Merchant upload). null = giữ nguyên, chuỗi rỗng = xóa. Chỉ áp dụng khi Admin duyệt.
        /// </summary>
        public string? BusinessLicense { get; set; }
        public Guid? BusinessLicenseDocumentId { get; set; }
        public bool RemoveBusinessLicenseDocument { get; set; }
        public Guid? OperationId { get; set; }
    }

    public class RejectMerchantProfileRequestCommand
    {
        public string? Reason { get; set; }
        public string? ExpectedVersion { get; set; }
        public Guid? OperationId { get; set; }
    }
}
