using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Commands
{
    public class CreateMerchantCommand
    {
        public Guid UserId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public decimal DepositAmount { get; set; } = 1000000;
        /// <summary>Để trống khi tạo mới thì dùng % hoa hồng mặc định (UC 3.9.2); để trống khi sửa thì giữ nguyên.</summary>
        public decimal? CommissionRate { get; set; }
        public int ServiceRadiusKm { get; set; } = 5;
        public int MaxCapacity { get; set; } = 50;
        public List<Guid> ServiceAreaIds { get; set; } = [];
    }

    public class UpdateMerchantCommand : CreateMerchantCommand
    {
        public Guid Id { get; set; }
        public MerchantStatus? Status { get; set; }
    }

    public class UpdateMerchantStatusCommand
    {
        public Guid MerchantId { get; set; }
        public MerchantStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public Guid? ApprovedBy { get; set; }
    }
}

