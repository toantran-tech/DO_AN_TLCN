using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Commands;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Queries;
using WashGo.Application.Contracts.Features.ServiceProposalContracts.Services;
using WashGo.Application.Contracts.Features.WashServiceContracts.Queries;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.ServiceProposal
{
    public class ServiceProposalHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var merchant = app.MapGroup("/merchant-service")
                .WithTags("Merchant Service - Dịch vụ của tiệm & đề xuất")
                .RequireAuthorization("MerchantOnly");

            merchant.MapPost("/filter", async (WashServiceFilterQuery query, IServiceProposalAppService svc) =>
                (await svc.GetMyServicesAsync(query)).CustomResult())
                .WithName("Merchant xem dịch vụ của tiệm")
                .WithSummary("Danh sách dịch vụ đã được Admin phê duyệt của tiệm (tên, mô tả, giá, thời gian xử lý, danh mục, trạng thái).")
                .WithOpenApi();

            merchant.MapPost("/proposals", async (ServiceProposalCommand command, IServiceProposalAppService svc) =>
                (await svc.ProposeCreateAsync(command)).CustomResult())
                .WithName("Merchant đề xuất dịch vụ mới")
                .WithSummary("Gửi đề xuất thêm dịch vụ (chờ Admin duyệt); kiểm tra trùng tên và giá hợp lệ, báo Admin.")
                .WithOpenApi();

            merchant.MapPost("/{serviceId:guid}/proposals", async (Guid serviceId, ServiceProposalCommand command, IServiceProposalAppService svc) =>
                (await svc.ProposeUpdateAsync(serviceId, command)).CustomResult())
                .WithName("Merchant đề xuất chỉnh sửa dịch vụ")
                .WithSummary("Đề xuất sửa tên, mô tả, giá, thời gian xử lý của dịch vụ thuộc tiệm; mỗi dịch vụ tối đa một đề xuất chờ duyệt.")
                .WithOpenApi();

            var proposals = app.MapGroup("/service-proposal")
                .WithTags("Service Proposal - Duyệt đề xuất dịch vụ");

            proposals.MapPost("/filter", async (ServiceProposalFilterQuery query, IServiceProposalAppService svc) =>
                (await svc.GetListAsync(query)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Danh sách đề xuất dịch vụ")
                .WithSummary("Admin xem mọi đề xuất (lọc trạng thái, loại, Merchant; chờ duyệt lên đầu); Merchant chỉ xem đề xuất của mình.")
                .WithOpenApi();

            proposals.MapGet("/{id:guid}", async (Guid id, IServiceProposalAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Chi tiết đề xuất dịch vụ")
                .WithSummary("Dữ liệu đề xuất kèm dữ liệu hiện tại của dịch vụ (với đề xuất chỉnh sửa).")
                .WithOpenApi();

            proposals.MapPost("/{id:guid}/approve", async (Guid id, IServiceProposalAppService svc) =>
                (await svc.ApproveAsync(id)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Phê duyệt đề xuất dịch vụ")
                .WithSummary("Áp dụng đề xuất: tạo dịch vụ mới (Đã duyệt) hoặc cập nhật dịch vụ; báo Merchant.")
                .WithOpenApi();

            proposals.MapPost("/{id:guid}/reject", async (Guid id, RejectServiceProposalCommand command, IServiceProposalAppService svc) =>
                (await svc.RejectAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Từ chối đề xuất dịch vụ")
                .WithSummary("Từ chối kèm lý do bắt buộc; báo Merchant.")
                .WithOpenApi();
        }
    }
}
