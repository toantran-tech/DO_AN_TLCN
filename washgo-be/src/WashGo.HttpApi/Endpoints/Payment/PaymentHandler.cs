using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using System.Linq;
using System.Net;
using WashGo.Application.Contracts.Features.PaymentContracts.Commands;
using WashGo.Application.Contracts.Features.PaymentContracts.Services;
using WashGo.Core.Endpoint;
using WashGo.Domain.Shared.Enums;
using WashGo.Application.Contracts.Features.PaymentContracts.Queries;

namespace WashGo.HttpApi.Endpoints.Payment
{
    public class PaymentHandler : IEndpointBase
    {
        private const string Endpoint = "/payment";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(Endpoint)
                .WithTags("Payment - Thanh toán");

            group.MapPost("/create-url", async (CreatePaymentCommand command, HttpContext context, IPaymentAppService service) =>
            {
                command.ClientIpAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
                return (await service.CreatePaymentAsync(command)).CustomResult();
            })
                .RequireAuthorization("CustomerOnly")
                .WithName("Tạo URL thanh toán")
                .WithSummary("Customer tạo giao dịch và URL thanh toán tiền giặt cho đơn hàng của mình.")
                .WithOpenApi();

            group.MapPost("/cancellation-fee/create-url", async (CreatePaymentCommand command, HttpContext context, IPaymentAppService service) =>
            {
                command.ClientIpAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
                return (await service.CreateCancellationFeePaymentAsync(command)).CustomResult();
            })
                .RequireAuthorization("CustomerOnly")
                .WithName("Thanh toán phí hủy đơn")
                .WithSummary("Customer tạo giao dịch phí hủy riêng; không thanh toán tiền giặt hoặc đổi đơn sang Paid.")
                .WithOpenApi();

            group.MapPost("/callback", async (PaymentCallbackCommand command, IPaymentAppService service) =>
                (await service.HandleCallbackAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Payment callback sandbox")
                .WithSummary("Admin mô phỏng kết quả thanh toán trong môi trường sandbox.")
                .WithOpenApi();

            group.MapGet("/vnpay-ipn", async (HttpRequest request, IPaymentAppService service) =>
            {
                var parameters = request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());
                return Results.Ok(await service.HandleVnPayIpnAsync(parameters));
            })
            .AllowAnonymous()
            .WithName("VNPay IPN")
            .WithSummary("Nhận thông báo thanh toán từ VNPay và kiểm tra chữ ký cùng thông tin giao dịch.")
            .WithOpenApi();

            group.MapPost("/momo-ipn", async (MomoIpnCommand ipn, IPaymentAppService service) =>
                await service.HandleMomoIpnAsync(ipn) ? Results.NoContent() : Results.BadRequest())
            .AllowAnonymous()
            .WithName("MoMo IPN")
            .WithSummary("Nhận kết quả thanh toán từ MoMo (kiểm tra chữ ký HMAC SHA256, số tiền); trả 204 để MoMo ngừng gửi lại.")
            .WithOpenApi();

            group.MapPost("/refund-requests", async (CreateRefundRequestCommand command, IPaymentAppService service) =>
                (await service.CreateRefundRequestAsync(command)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Tạo yêu cầu hoàn tiền")
                .WithSummary("Customer gửi yêu cầu hoàn tiền cho đơn hàng của mình.")
                .WithOpenApi();

            group.MapPost("/overdue-fee/create-url", async (CreateOverdueFeePaymentCommand command, HttpContext context, IPaymentAppService service) =>
            {
                command.ClientIpAddress = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "127.0.0.1";
                return (await service.CreateOverdueFeePaymentAsync(command)).CustomResult();
            })
                .RequireAuthorization("CustomerOnly")
                .WithName("Tạo URL thanh toán phí lưu kho quá hạn")
                .WithSummary("Customer tạo giao dịch thanh toán phí quá hạn riêng với tiền giặt.")
                .WithOpenApi();

            group.MapGet("/refund-requests/mine", async (IPaymentAppService service) =>
                (await service.GetRefundRequestsAsync(mineOnly: true)).CustomResult())
                .RequireAuthorization("CustomerOnly")
                .WithName("Xem yêu cầu hoàn tiền của tôi")
                .WithSummary("Xem yêu cầu hoàn tiền của tôi.")
                .WithOpenApi();

            group.MapGet("/refund-requests", async (RefundRequestStatus? status, IPaymentAppService service) =>
                (await service.GetRefundRequestsAsync(mineOnly: false, status)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Admin xem danh sách yêu cầu hoàn tiền")
                .WithSummary("Admin xem yêu cầu hoàn tiền (mã đơn, khách hàng, số tiền, lý do, ngày, trạng thái), lọc theo trạng thái 1-4.")
                .WithOpenApi();

            group.MapGet("/refund-requests/{id:guid}", async (Guid id, IPaymentAppService service) =>
                (await service.GetRefundRequestAsync(id)).CustomResult())
                .RequireAuthorization()
                .WithName("Xem chi tiết yêu cầu hoàn tiền")
                .WithSummary("Admin xem mọi yêu cầu; Customer chỉ xem yêu cầu của mình.")
                .WithOpenApi();
            group.MapPost("/refund-requests/filter", async (RefundFilterQuery query, IPaymentAppService service) => (await service.FilterRefundRequestsAsync(query)).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("Lọc yêu cầu hoàn tiền tại DB: trạng thái/đơn/khách/ngày/số tiền/keyword; phân trang ổn định.").WithOpenApi();

            group.MapPost("/refund-requests/{id:guid}/review", async (Guid id, ReviewRefundRequestCommand command, IPaymentAppService service) =>
                (await service.ReviewRefundRequestAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Admin duyệt hoặc từ chối yêu cầu hoàn tiền")
                .WithSummary("Admin duyệt hoặc từ chối yêu cầu hoàn tiền.")
                .WithOpenApi();

            group.MapPost("/refund-requests/{id:guid}/confirm-refunded", async (Guid id, ConfirmRefundCommand command, IPaymentAppService service) =>
                (await service.ConfirmRefundedAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Admin xác nhận đã hoàn tiền")
                .WithSummary("Admin ghi nhận đã thực hiện hoàn tiền cho yêu cầu được duyệt.")
                .WithOpenApi();
        }
    }
}
