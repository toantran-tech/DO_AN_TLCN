using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.LoyaltyContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Customer;

public class LoyaltyHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customer/loyalty").WithTags("Customer - Điểm thưởng").RequireAuthorization("CustomerOnly");
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/balance", async (ILoyaltyAppService service) => (await service.GetBalanceAsync()).CustomResult())
            .WithSummary("Xem tổng điểm, điểm giữ, khả dụng và nợ điểm; 1 điểm = 1đ. Nợ điểm chỉ chặn đổi điểm.").WithOpenApi();
        group.MapGet("/history", async (int? page, int? take, ILoyaltyAppService service) =>
            (await service.GetHistoryAsync(page ?? 1, take ?? 20)).CustomResult())
            .WithSummary("Lịch sử điểm của chính Customer; page từ 1, take tối đa 100, mới nhất trước.").WithOpenApi();
        group.MapGet("/quote/{orderId:guid}", async (Guid orderId, long? pointsToRedeem, ILoyaltyAppService service) =>
            (await service.QuoteAsync(orderId, pointsToRedeem ?? 0)).CustomResult())
            .WithSummary("Báo giá dùng điểm cho đơn của mình đã giặt xong; không giữ điểm. Gửi cùng pointsToRedeem khi tạo URL thanh toán.").WithOpenApi();
    }
}
