using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using WashGo.Application.Contracts.Features.FinanceContracts.Commands;
using WashGo.Application.Contracts.Features.FinanceContracts.Queries;
using WashGo.Application.Contracts.Features.FinanceContracts.Results;
using WashGo.Core.Domain.Shared.Bases;

namespace WashGo.Application.Contracts.Features.FinanceContracts.Services
{
    public interface IFinanceAppService : IApplicationService
    {
        Task<BaseResponse<RevenueReportDto>> GetRevenueReportAsync(FinancePeriodQuery query);
        Task<BaseResponse<string>> ExportRevenueReportCsvAsync(FinancePeriodQuery query);
        Task<BaseResponse<List<MerchantCommissionDto>>> GetCommissionsAsync(FinancePeriodQuery query);
        Task<BaseResponse<SettlementDto>> CreateSettlementAsync(CreateSettlementCommand command);
        Task<BaseResponse<SettlementQuoteDto>> QuoteSettlementAsync(CreateSettlementCommand command);
        Task<BaseResponse<WashGo.Application.Contracts.Features.AuditContracts.Results.AdministrativeOperationDto>> GetOperationAsync(Guid id);
        Task<PaginatedResult<SettlementDto>> GetSettlementsAsync(SettlementFilterQuery query);
        Task<BaseResponse<SettlementBreakdownDto>> GetSettlementBreakdownAsync(Guid id);
        Task<BaseResponse<SettlementDto>> GetSettlementAsync(Guid id);
        Task<PaginatedResult<SettlementOrderLineDto>> GetSettlementLinesAsync(Guid id, int page, int take);
        Task<BaseResponse<SettlementDto>> ConfirmSettlementPaidAsync(Guid id, ConfirmSettlementPaidCommand command);
    }
}
