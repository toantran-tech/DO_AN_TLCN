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
        public Guid MerchantId { get; set; }
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
        public Guid Id { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class UpdateWashServiceStatusCommand
    {
        public Guid ServiceId { get; set; }
        public ServiceApprovalStatus ApprovalStatus { get; set; }
        public bool IsActive { get; set; } = true;
        public string? RejectionReason { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Queries
{
    public class WashServiceFilterQuery : BaseFilterQuery
    {
        public WashServiceType? ServiceType { get; set; }
        public bool? IsActive { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Results
{
    public class WashServiceResultDto
    {
        public Guid Id { get; set; }
        public Guid MerchantId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? PricePerKg { get; set; }
        public decimal? PricePerItem { get; set; }
        public WashServiceType ServiceType { get; set; }
        public string Unit { get; set; } = string.Empty;
        public int EstimatedDurationHours { get; set; }
        public ServiceApprovalStatus ApprovalStatus { get; set; }
        public bool IsActive { get; set; }
        public long CreatedOn { get; set; }
    }
}

namespace WashGo.Application.Contracts.Features.WashServiceContracts.Services
{
    public interface IWashServiceAppService : IApplicationService
    {
        Task<PaginatedResult<Results.WashServiceResultDto>> GetListAsync(Queries.WashServiceFilterQuery query);
        Task<BaseResponse<Results.WashServiceResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<Results.WashServiceResultDto>> CreateAsync(Commands.CreateWashServiceCommand command);
        Task<BaseResponse<Results.WashServiceResultDto>> UpdateAsync(Commands.UpdateWashServiceCommand command);
        Task<BaseResponse<Results.WashServiceResultDto>> UpdateStatusAsync(Commands.UpdateWashServiceStatusCommand command);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, Queries.WashServiceFilterQuery query);
    }
}