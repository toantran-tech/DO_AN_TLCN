using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Commands
{
    public class CreateWashServiceCommand
    {
        public Guid? OperationId { get; set; }
        public List<string>? ImageUrls { get; set; }
        /// <summary>Tùy chọn: Merchant đề xuất dịch vụ. Dịch vụ thuộc bảng giá chung của nền tảng.</summary>
        public Guid? MerchantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? PricePerKg { get; set; }
        public decimal? PricePerItem { get; set; }
        public WashServiceType ServiceType { get; set; } = WashServiceType.StandardWash;
        public string Unit { get; set; } = "kg";
        public int EstimatedDurationHours { get; set; } = 24;
    }

    public class UpdateWashServiceCommand : CreateWashServiceCommand
    {
        public string? ExpectedVersion { get; set; }
        public Guid Id { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateWashServiceStatusCommand
    {
        public Guid? OperationId { get; set; }
        public string? ExpectedVersion { get; set; }
        public Guid ServiceId { get; set; }
        public ServiceApprovalStatus ApprovalStatus { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RejectionReason { get; set; }
    }
}

