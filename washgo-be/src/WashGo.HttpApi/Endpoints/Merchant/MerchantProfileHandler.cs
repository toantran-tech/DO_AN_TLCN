using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Commands;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Queries;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Results;
using WashGo.Application.Contracts.Features.MerchantProfileContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Merchant
{
    public class MerchantProfileHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var merchant = app.MapGroup("/merchant-profile")
                .WithTags("Merchant Profile - Hồ sơ cửa hàng")
                .RequireAuthorization("MerchantOnly");

            merchant.MapGet("/", async (IMerchantProfileAppService svc) =>
                (await svc.GetMyProfileAsync()).CustomResult())
                .WithName("Merchant xem hồ sơ cửa hàng")
                .WithSummary("Thông tin cửa hàng, ảnh, hợp đồng và yêu cầu cập nhật đang chờ duyệt (nếu có).")
                .WithOpenApi();

            merchant.MapPost("/update-requests", async (MerchantProfileUpdateCommand command, IMerchantProfileAppService svc) =>
                (await svc.RequestUpdateAsync(command)).CustomResult())
                .WithName("Merchant gửi yêu cầu cập nhật hồ sơ")
                .WithSummary("Gửi tên cửa hàng, địa chỉ, SĐT, giờ mở cửa, hình ảnh để Admin duyệt; mỗi lúc tối đa một yêu cầu chờ duyệt.")
                .WithOpenApi();

            var requests = app.MapGroup("/merchant-profile-request")
                .WithTags("Merchant Profile Request - Duyệt cập nhật hồ sơ");

            requests.MapPost("/filter", async (MerchantProfileRequestFilterQuery query, IMerchantProfileAppService svc) =>
                (await svc.GetRequestsAsync(query)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Danh sách yêu cầu cập nhật hồ sơ")
                .WithSummary("Admin xem mọi yêu cầu (lọc trạng thái, Merchant; chờ duyệt lên đầu); Merchant chỉ xem của mình.")
                .WithOpenApi();

            requests.MapGet("/{id:guid}", async (Guid id, IMerchantProfileAppService svc) =>
                (await svc.GetRequestAsync(id)).CustomResult())
                .RequireAuthorization("MerchantOrAdmin")
                .WithName("Chi tiết yêu cầu cập nhật hồ sơ")
                .WithSummary("Dữ liệu đề xuất, dữ liệu hiện tại và các trường thay đổi.")
                .WithOpenApi();

            requests.MapPost("/{id:guid}/approve", async (Guid id, string? expectedVersion, Guid? operationId, IMerchantProfileAppService svc) =>
                (await svc.ApproveAsync(id, expectedVersion, operationId)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Phê duyệt cập nhật hồ sơ Merchant")
                .WithSummary("Admin duyệt: query expectedVersion bắt buộc từ chi tiết yêu cầu, operationId tùy chọn để gửi lại an toàn. Hồ sơ đã đổi từ lúc Merchant gửi → 409 MerchantProfileChanged; không áp snapshot cũ.")
                .WithOpenApi();

            requests.MapPost("/{id:guid}/reject", async (Guid id, RejectMerchantProfileRequestCommand command, IMerchantProfileAppService svc) =>
                (await svc.RejectAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Từ chối cập nhật hồ sơ Merchant")
                .WithSummary("Từ chối: body reason, expectedVersion bắt buộc; operationId tùy chọn. Gửi lại cùng key/nội dung trả kết quả cũ, khác nội dung → 409.")
                .WithOpenApi();
        }
    }
}
