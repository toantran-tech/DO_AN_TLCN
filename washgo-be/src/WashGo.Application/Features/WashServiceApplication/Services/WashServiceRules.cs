using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using WashGo.Domain.Entities.WashService;
using WashGo.Domain.Entities.WashService.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashServiceApplication.Services
{
    /// <summary>Kiểm tra dùng chung cho Admin tạo/sửa dịch vụ (UC 3.5) và Merchant đề xuất (UC 2.6).</summary>
    public static class WashServiceRules
    {
        public const string InvalidPrice = "Giá dịch vụ không hợp lệ.";
        public const string DuplicateName = "Tên dịch vụ này đã tồn tại.";

        public static string? Validate(string? name, decimal unitPrice, decimal? pricePerKg, decimal? pricePerItem,
            int durationHours, WashServiceType serviceType, string? unit)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
                return "Tên dịch vụ phải có từ 1 đến 200 ký tự.";
            if (unitPrice <= 0 || unitPrice > 100_000_000 || pricePerKg is <= 0 || pricePerItem is <= 0)
                return InvalidPrice;
            if (durationHours is < 1 or > 8760)
                return "Thời gian xử lý phải từ 1 đến 8760 giờ.";
            if (!Enum.IsDefined(serviceType))
                return "Danh mục dịch vụ không hợp lệ.";
            if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 20)
                return "Đơn vị tính không hợp lệ.";
            return null;
        }

        /// <summary>Tên dịch vụ không trùng trong cùng một Merchant (không phân biệt hoa thường, bỏ qua dịch vụ đã xóa).</summary>
        public static Task<bool> NameTakenAsync(IWashServiceRepository repo, Guid merchantId, string name, Guid? exceptId)
        {
            var normalized = name.Trim().ToLower();
            return repo.AnyAsync(x => x.MerchantId == merchantId && !x.IsDeleted && x.Id != exceptId &&
                                      x.Name.ToLower() == normalized);
        }
    }
}
