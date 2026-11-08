using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Commands;
using WashGo.Application.Contracts.Features.SystemSettingContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.SystemSetting
{
    public class SystemSettingHandler : IEndpointBase
    {
        private const string _endpoint = "/system-setting";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("System Setting - Cài đặt hệ thống")
            .RequireAuthorization("AdminOnly");

            group.MapGet("/", async (ISystemSettingAppService svc) =>
                (await svc.GetAsync()).CustomResult())
                .WithName("Xem cài đặt hệ thống")
                .WithSummary("Admin xem SLA, phí hoa hồng mặc định, phí quá hạn/lưu kho, phí hủy và mức bồi thường hiện hành.")
                .WithOpenApi();
            group.MapGet("/operations/{id:guid}", async (Guid id, ISystemSettingAppService svc) => (await svc.GetOperationAsync(id)).CustomResult())
                .WithSummary("Tra kết quả lưu cấu hình theo operationId của chính Admin; 404 chưa xác định được yêu cầu đang chạy.").WithOpenApi();
            group.MapPost("/history/filter", async (WashGo.Application.Contracts.Features.AuditContracts.Queries.AuditLogFilterQuery query, ISystemSettingAppService svc) => (await svc.GetHistoryAsync(query)).CustomResult())
                .WithSummary("Lịch sử cấu hình append-only; lọc actor/thời gian/action=UpdateSla|UpdateCommission|UpdatePenalty, phân trang.").WithOpenApi();

            group.MapPut("/sla", async (UpdateSlaSettingsCommand command, ISystemSettingAppService svc) =>
                (await svc.UpdateSlaAsync(command)).CustomResult())
                .WithName("Cấu hình SLA")
                .WithSummary("Thời gian gửi đồ tối đa (phút), Merchant lấy đồ/trả đồ tối đa (giờ), thời gian khách lấy đồ miễn phí (giờ).")
                .WithOpenApi();

            group.MapPut("/commission", async (UpdateCommissionSettingsCommand command, ISystemSettingAppService svc) =>
                (await svc.UpdateCommissionAsync(command)).CustomResult())
                .WithName("Cấu hình phí hoa hồng")
                .WithSummary("% phí hoa hồng mặc định (0-100) áp dụng cho Merchant mới không nhập tỷ lệ riêng.")
                .WithOpenApi();

            group.MapPut("/penalty", async (UpdatePenaltySettingsCommand command, ISystemSettingAppService svc) =>
                (await svc.UpdatePenaltyAsync(command)).CustomResult())
                .WithName("Cấu hình phí phạt và bồi thường")
                .WithSummary("Phí quá hạn locker theo ngày 1/2/3, phí lưu kho/ngày, % phí hủy theo giai đoạn và % bồi thường theo mức hư hỏng.")
                .WithOpenApi();
        }
    }
}
