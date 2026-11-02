using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.MerchantProfileContracts
{
    /// <summary>Các trường Merchant được tự sửa (UC 2.1.2); ImageUrls = null giữ nguyên ảnh hiện tại.</summary>
    public class MerchantProfileUpdateCommand
    {
        public string BusinessName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public List<string>? ImageUrls { get; set; }
    }

    public class RejectMerchantProfileRequestCommand
    {
        public string? Reason { get; set; }
    }

    public class MerchantProfileRequestFilterQuery
    {
        public ServiceApprovalStatus? Status { get; set; }
        public Guid? MerchantId { get; set; }
        public int Page { get; set; } = 1;
        public int Take { get; set; } = 20;
    }

    public class MerchantProfileDataDto
    {
        public string BusinessName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public List<string> ImageUrls { get; set; } = [];
    }

    public class MerchantProfileRequestDto
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public string MerchantName { get; set; } = string.Empty;
        public ServiceApprovalStatus Status { get; set; }
        public MerchantProfileDataDto Proposed { get; set; } = new();
        public MerchantProfileDataDto Current { get; set; } = new();
        public List<string> ChangedFields { get; set; } = [];
        public string? RejectionReason { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }

    public class MerchantProfileDto
    {
        public Guid Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public List<string> ImageUrls { get; set; } = [];
        public MerchantStatus Status { get; set; }
        public decimal Rating { get; set; }
        public int TotalOrders { get; set; }
        public decimal CommissionRate { get; set; }
        public int ServiceRadiusKm { get; set; }
        public int MaxCapacity { get; set; }
        public DateTime? ContractStartDate { get; set; }
        public DateTime? ContractEndDate { get; set; }
        public MerchantProfileRequestDto? PendingRequest { get; set; }
    }

    public interface IMerchantProfileAppService
    {
        Task<BaseResponse<MerchantProfileDto>> GetMyProfileAsync();
        Task<BaseResponse<MerchantProfileRequestDto>> RequestUpdateAsync(MerchantProfileUpdateCommand command);
        Task<PaginatedResult<MerchantProfileRequestDto>> GetRequestsAsync(MerchantProfileRequestFilterQuery query);
        Task<BaseResponse<MerchantProfileRequestDto>> GetRequestAsync(Guid id);
        Task<BaseResponse<MerchantProfileRequestDto>> ApproveAsync(Guid id);
        Task<BaseResponse<MerchantProfileRequestDto>> RejectAsync(Guid id, RejectMerchantProfileRequestCommand command);
    }
}
