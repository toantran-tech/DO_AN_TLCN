using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.ShipperContracts.Commands;
using WashGo.Application.Contracts.Features.ShipperContracts.Queries;
using WashGo.Application.Contracts.Features.ShipperContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Shipper
{
    public class ShipperHandler : IEndpointBase
    {
        private const string _endpoint = "/shipper";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("Shipper - Quản lý Shipper")
            .RequireAuthorization("MerchantOrAdmin");

            group.MapPost("/filter", async (ShipperFilterQuery query, IShipperManagementAppService svc) =>
                (await svc.GetListAsync(query)).CustomResult())
                .WithName("Lọc danh sách Shipper")
                .WithSummary("Merchant xem Shipper của tiệm (họ tên, avatar, SĐT, phương tiện, biển số, trạng thái, số đơn đã giao, đánh giá); Admin lọc được theo Merchant. sortBy: [{field:\"Rating\", isDescending:true}] sắp xếp toàn bộ kết quả trước khi phân trang, Shipper chưa có đánh giá đứng cuối.")
                .WithOpenApi();

            group.MapGet("/summary", async (Guid? merchantId, IShipperManagementAppService svc) =>
                (await svc.GetSummaryAsync(merchantId)).CustomResult())
                .WithName("Thống kê đội Shipper")
                .WithSummary("Tổng, đang làm việc, tạm nghỉ, sẵn sàng, đang bận, offline của toàn đội (không theo bộ lọc bảng), một lần đếm. Merchant lấy theo JWT; merchantId chỉ dành cho Admin.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IShipperManagementAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết Shipper")
                .WithSummary("Xem chi tiết Shipper thuộc tiệm của mình.")
                .WithOpenApi();

            group.MapPost("/", async (CreateShipperCommand command, HttpContext http, IShipperManagementAppService svc) =>
            {
                if (command.ClientRequestId == null && Guid.TryParse(http.Request.Headers["Idempotency-Key"].ToString(), out var key))
                    command.ClientRequestId = key;
                return (await svc.CreateAsync(command)).CustomResult();
            })
                .WithName("Thêm mới Shipper")
                .WithSummary("Tạo tài khoản role Shipper cho tiệm; mật khẩu tạm được gửi qua email (không trả về trong API), kết quả gửi ở credentialDelivery/credentialEmailSent. clientRequestId (hoặc Idempotency-Key): gửi lại trả Shipper đã tạo (200, meta.replayed), không tạo thêm/không gửi lại email. Ảnh CCCD: identityFrontImageUrl + identityBackImageUrl.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/lock", async (Guid id, ShipperAccountActionCommand command, IShipperManagementAppService svc) =>
                (await svc.LockAsync(id, command)).CustomResult())
                .WithName("Khóa tài khoản Shipper")
                .WithSummary("Khóa tài khoản đăng nhập (User) của Shipper trong tiệm, lý do bắt buộc. Thu hồi mọi phiên; access token cũ bị từ chối ngay; không đăng nhập/nhận phân công được. Còn nhiệm vụ lấy/trả chưa xong → 409 HasActiveTasks. Khóa lại tài khoản đã khóa → 200 AlreadyLocked. Có audit.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/unlock", async (Guid id, ShipperAccountActionCommand command, IShipperManagementAppService svc) =>
                (await svc.UnlockAsync(id, command)).CustomResult())
                .WithName("Mở khóa tài khoản Shipper")
                .WithSummary("Mở khóa tài khoản; không tự đổi trạng thái làm việc/vận hành, không khôi phục nhiệm vụ cũ. Có audit.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/tasks", async (Guid id, ShipperTaskQuery query, IShipperManagementAppService svc) =>
            {
                var result = await svc.GetTasksAsync(id, query);
                return result.StatusCode == System.Net.HttpStatusCode.NotFound ? Results.NotFound(result) : result.CustomResult();
            })
                .WithName("Lịch sử nhiệm vụ của Shipper")
                .WithSummary("Merchant xem nhiệm vụ lấy/trả của một Shipper trong tiệm (cả Shipper đã xóa): mã đơn, loại, trạng thái nhiệm vụ (Assigned/Completed/Reassigned/Cancelled), mốc phân công/hoàn tất/kết thúc thật; lọc taskType/status, phân trang tại BE.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/audits", async (Guid id, IShipperManagementAppService svc) =>
                (await svc.GetAuditsAsync(id)).CustomResult())
                .WithName("Lịch sử thao tác quản trị Shipper")
                .WithSummary("Ai đã khóa/mở khóa/xóa/sửa giấy tờ Shipper, lúc nào, lý do (mới nhất trước).")
                .WithOpenApi();

            group.MapDelete("/{id:guid}", async (Guid id, string? reason, IShipperManagementAppService svc) =>
                (await svc.DeleteAsync(id, reason)).CustomResult())
                .WithName("Xóa Shipper")
                .WithSummary("Xóa mềm Shipper của tiệm (?reason= bắt buộc): ẩn khỏi danh sách/thống kê/phân công, khóa đăng nhập và thu hồi phiên; giữ đơn hàng và lịch sử giao nhận. Còn nhiệm vụ chưa xong → 409 HasActiveTasks. Xóa lại → 200 AlreadyDeleted. Có audit.")
                .WithOpenApi();

            group.MapPost("/batch/lock", async (ShipperBatchActionCommand command, IShipperManagementAppService svc) =>
                (await svc.BatchLockAsync(command)).CustomResult())
                .WithName("Khóa nhiều Shipper")
                .WithSummary("Khóa tối đa 50 Shipper (ID trùng bỏ qua), mỗi người một transaction theo cùng luật khóa đơn lẻ; trả kết quả/lý do theo từng ID (thành công từng phần). Gửi lại an toàn.")
                .WithOpenApi();

            group.MapPost("/batch/delete", async (ShipperBatchActionCommand command, IShipperManagementAppService svc) =>
                (await svc.BatchDeleteAsync(command)).CustomResult())
                .WithName("Xóa nhiều Shipper")
                .WithSummary("Xóa mềm tối đa 50 Shipper (ID trùng bỏ qua), mỗi người một transaction theo cùng luật xóa đơn lẻ; trả kết quả/lý do theo từng ID. Gửi lại an toàn.")
                .WithOpenApi();

            group.MapPut("/{id:guid}", async (Guid id, UpdateShipperCommand command, HttpContext http, IShipperManagementAppService svc) =>
            {
                if (command.ExpectedRevision == null && long.TryParse(http.Request.Headers.IfMatch.ToString().Trim().Trim('"'), out var revision))
                    command.ExpectedRevision = revision;
                return (await svc.UpdateAsync(id, command)).CustomResult();
            })
                .WithName("Chỉnh sửa Shipper")
                .WithSummary("Cập nhật họ tên, SĐT, email, phương tiện, biển số và trạng thái Đang làm việc/Tạm nghỉ.")
                .WithOpenApi();
        }
    }
}
