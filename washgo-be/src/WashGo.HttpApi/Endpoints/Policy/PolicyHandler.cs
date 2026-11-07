using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.PolicyContracts.Commands;
using WashGo.Application.Contracts.Features.PolicyContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Policy
{
    public class PolicyHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/policy").WithTags("Policy - Điều khoản dịch vụ");

            group.MapGet("/current", async (IPolicyAppService svc) =>
                (await svc.GetCurrentAsync()).CustomResult())
                .AllowAnonymous()
                .WithName("Điều khoản và quy định hiện hành")
                .WithSummary("Công khai: phiên bản điều khoản, nội dung, ngày hiệu lực và quy định đang áp dụng (hạn gửi đồ, % phí hủy, phí quá hạn/lưu kho, bồi thường).")
                .WithOpenApi();

            group.MapGet("/customer", async (IPolicyAppService svc) =>
                (await svc.GetForCustomerAsync()).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Điều khoản của Customer")
                .WithSummary("Điều khoản hiện hành kèm trạng thái đã đồng ý của tài khoản (accepted, acceptedAt, requiresAcceptance) — đồng bộ mọi thiết bị.")
                .WithOpenApi();

            group.MapPost("/customer/acknowledge", async (AcknowledgeTermsCommand command, HttpContext context, IPolicyAppService svc) =>
            {
                command.IpAddress = context.Connection.RemoteIpAddress?.ToString();
                command.UserAgent = context.Request.Headers.UserAgent.ToString();
                return (await svc.AcknowledgeAsync(command)).CustomResult();
            })
                .RequireAuthorization("CustomerOnly")
                .WithName("Đồng ý điều khoản")
                .WithSummary("Customer đồng ý đúng phiên bản đang hiển thị; lưu thời điểm, IP, trình duyệt. Gọi lại không tạo bản ghi mới.")
                .WithOpenApi();

            group.MapGet("/versions", async (IPolicyAppService svc) =>
                (await svc.GetVersionsAsync()).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Các phiên bản điều khoản")
                .WithOpenApi();

            group.MapPost("/", async (PublishTermsCommand command, IPolicyAppService svc) =>
                (await svc.PublishAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Phát hành điều khoản mới")
                .WithSummary("Admin phát hành phiên bản mới (version, title, content, effectiveFrom Unix giây); Customer phải đồng ý lại khi có hiệu lực.")
                .WithOpenApi();
        }
    }
}
