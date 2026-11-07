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
        public const string InvalidPrice = "Giá dịch vụ không hợp lệ (lớn hơn 0, tối đa 100.000.000đ, tối đa 2 chữ số thập phân).";
        public const string DuplicateName = "Tên dịch vụ này đã tồn tại.";

        public static string? Validate(string? name, decimal unitPrice, decimal? pricePerKg, decimal? pricePerItem,
            int durationHours, WashServiceType serviceType, string? unit)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
                return "Tên dịch vụ phải có từ 1 đến 200 ký tự.";
            // Every price: > 0, at most 100 million VND, at most 2 decimals.
            if (!ValidPrice(unitPrice) || pricePerKg is { } kg && !ValidPrice(kg) || pricePerItem is { } item && !ValidPrice(item))
                return InvalidPrice;
            if (durationHours is < 1 or > 8760)
                return "Thời gian xử lý phải từ 1 đến 8760 giờ.";
            if (!Enum.IsDefined(serviceType))
                return "Danh mục dịch vụ không hợp lệ.";
            if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > 20)
                return "Đơn vị tính không hợp lệ.";
            return null;
        }

        public const decimal MaxPrice = 100_000_000m;

        public static bool ValidPrice(decimal price) => price > 0 && price <= MaxPrice && decimal.Round(price, 2) == price;

        /// <summary>Tên dịch vụ không trùng trong bảng dịch vụ chung (không phân biệt hoa thường, bỏ qua dịch vụ đã xóa).</summary>
        public static Task<bool> NameTakenAsync(IWashServiceRepository repo, string name, Guid? exceptId)
        {
            var normalized = name.Trim().ToLower();
            return repo.AnyAsync(x => !x.IsDeleted && x.Id != exceptId && x.Name.ToLower() == normalized);
        }
    }
}
