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
    }

    public class RejectMerchantProfileRequestCommand
    {
        public string? Reason { get; set; }
    }
}
