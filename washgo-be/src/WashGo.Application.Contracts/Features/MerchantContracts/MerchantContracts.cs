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
        public decimal CommissionRate { get; set; } = 20;
        public int ServiceRadiusKm { get; set; } = 5;
        public int MaxCapacity { get; set; } = 50;
        public List<Guid> ServiceAreaIds { get; set; } = [];
    }

    public class UpdateMerchantCommand : CreateMerchantCommand
    {
        public Guid Id { get; set; }
        public MerchantStatus Status { get; set; }
    }

    public class UpdateMerchantStatusCommand
    {
        public Guid MerchantId { get; set; }
        public MerchantStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public Guid? ApprovedBy { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.MerchantContracts.Queries
{
    public class MerchantFilterQuery : BaseFilterQuery
    {
        public MerchantStatus? Status { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.MerchantContracts.Results
{
    public class MerchantResultDto
    {
        public Guid Id { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string? TaxCode { get; set; }
        public string Address { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string? OpeningHours { get; set; }
        public MerchantStatus Status { get; set; }
        public string? RejectionReason { get; set; }
        public decimal Rating { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal DepositAmount { get; set; }
        public decimal CommissionRate { get; set; }
        public int ServiceRadiusKm { get; set; }
        public int CurrentWorkload { get; set; }
        public int MaxCapacity { get; set; }
        public long CreatedOn { get; set; }
        public List<Guid> ServiceAreaIds { get; set; } = [];
    }
}

namespace WashGo.Application.Contracts.Features.MerchantContracts.Services
{
    public interface IMerchantAppService : IApplicationService
    {
        Task<PaginatedResult<Results.MerchantResultDto>> GetListAsync(Queries.MerchantFilterQuery query);
        Task<BaseResponse<Results.MerchantResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<Results.MerchantResultDto>> CreateAsync(Commands.CreateMerchantCommand command);
        Task<BaseResponse<Results.MerchantResultDto>> UpdateAsync(Commands.UpdateMerchantCommand command);
        Task<BaseResponse<Results.MerchantResultDto>> UpdateStatusAsync(Commands.UpdateMerchantStatusCommand command);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, Queries.MerchantFilterQuery query);
    }
}
