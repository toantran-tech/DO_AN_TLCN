using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Shared.Enums;

using WashGo.Application.Contracts.Features.MerchantContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantContracts.Results;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Services
{
    public interface IMerchantAppService : IApplicationService
    {
        Task<PaginatedResult<MerchantResultDto>> GetListAsync(MerchantFilterQuery query);
        Task<BaseResponse<MerchantResultDto>> GetByIdAsync(Guid id);
        Task<BaseResponse<MerchantResultDto>> CreateAsync(CreateMerchantCommand command);
        Task<BaseResponse<MerchantResultDto>> UpdateAsync(UpdateMerchantCommand command);
        Task<BaseResponse<MerchantResultDto>> UpdateStatusAsync(UpdateMerchantStatusCommand command);
        Task<BaseResponse<List<ColumnFilterDistinctValueDto>>> GetColumnDistinctValuesAsync(string fieldName, MerchantFilterQuery query);
    }
}
