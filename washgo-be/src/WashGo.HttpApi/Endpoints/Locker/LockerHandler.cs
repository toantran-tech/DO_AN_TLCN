using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.LockerContracts.Commands;
using WashGo.Application.Contracts.Features.LockerContracts.Queries;
using WashGo.Application.Contracts.Features.LockerContracts.Services;
using WashGo.Core.Endpoint;
using System.Net;
using WashGo.Application.Contracts.Features.AuditContracts.Commands;

namespace WashGo.HttpApi.Endpoints.Locker
{
    public class LockerHandler : IEndpointBase
    {
        private const string _endpoint = "/locker";

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup(_endpoint)
            .WithTags("Locker - Quản lý tủ thông minh")
            .RequireAuthorization("MerchantOrAdmin");

            group.MapPost("/filter", async (LockerFilterQuery request, ILockerAppService svc) =>
                (await svc.GetListAsync(request)).CustomResult())
                .WithName("Lọc danh sách Locker")
                .WithSummary("Admin xem mọi locker; Merchant chỉ xem locker được gán (tên, địa chỉ, số ô, số ô trống, trạng thái).")
                .WithOpenApi();

            group.MapGet("/summary", async (Guid? merchantId, ILockerAppService svc) =>
                (await svc.GetSummaryAsync(merchantId)).CustomResult())
                .WithName("Thống kê Locker")
                .WithSummary("Tổng locker được gán (đang hoạt động/bảo trì/ngừng) và tổng ô chưa xóa theo trạng thái (trống/có đồ/giữ chỗ/bảo trì), một lần đếm, không theo bộ lọc bảng. Merchant theo JWT; merchantId chỉ dành cho Admin.")
                .WithOpenApi();

            group.MapGet("/eligible-merchants", async (double? latitude, double? longitude, Guid? lockerId, string? keyword,
                bool? includeIneligible, int? page, int? take, ILockerAppService svc) =>
                (await svc.GetEligibleMerchantsAsync(latitude, longitude, lockerId, keyword, includeIneligible ?? false, page ?? 1, take ?? 20)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Merchant phù hợp vị trí locker")
                .WithSummary("Merchant có thể phụ trách locker theo tọa độ LOCKER (latitude/longitude; bỏ trống khi gửi lockerId thì dùng tọa độ đã lưu). Đủ điều kiện = đang hoạt động, tài khoản hợp lệ, hợp đồng còn hiệu lực và khoảng cách Haversine cửa hàng→locker (chưa làm tròn) ≤ serviceRadiusKm. lockerId: luôn trả cả Merchant đang gán (isLinked, linkPriority) dù không còn hợp lệ. includeIneligible=true trả thêm Merchant hoạt động ngoài bán kính/thiếu tọa độ với blockedReasonCode. Sắp: hợp lệ trước, gần trước, tên, id; lọc/phân trang ở BE. distanceKm làm tròn 2 số, null khi thiếu tọa độ. Lỗi 400 meta.code: LockerLocationRequired, InvalidLocation, InvalidFilter.")
                .WithOpenApi();

            group.MapGet("/coverage-issues", async (int? page, int? take, ILockerAppService svc) =>
                (await svc.GetCoverageIssuesAsync(page ?? 1, take ?? 50)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Liên kết Merchant–locker ngoài bán kính")
                .WithSummary("Liên kết cũ không đạt bán kính phục vụ (reasonCode MerchantOutsideServiceRadius/MerchantLocationUnavailable/LockerLocationRequired), khoảng cách, bán kính đề nghị, locker còn Merchant khác phục vụ hay không. Liên kết vẫn được giữ, đơn đang xử lý tiếp tục; liên kết này không nhận đơn mới cho tới khi Admin xử lý.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, ILockerAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết Locker")
                .WithSummary("Chi tiết locker và trạng thái từng ô (Trống/Đang giữ/Đang có đồ/Bảo trì, cờ hasItems); Merchant chỉ xem locker của mình.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/usage-history", async (Guid id, long? fromDate, long? toDate, Guid? boxId, int? page, int? take, ILockerAppService svc) =>
            {
                var result = await svc.GetUsageHistoryAsync(id, new LockerUsageQuery
                {
                    FromDate = fromDate, ToDate = toDate, BoxId = boxId, Page = page ?? 1, Take = take ?? 20
                });
                // Paged results map every failure to 400; a locker outside the caller's scope is 404.
                return result.StatusCode == HttpStatusCode.NotFound ? Results.NotFound(result) : result.CustomResult();
            })
                .WithName("Lịch sử sử dụng Locker")
                .WithSummary("Các lượt dùng ô (usageId, boxId, mã đơn, ô, gửi/trả, thời gian đặt vào/lấy ra, trạng thái đơn); lọc theo thời gian đặt vào và theo ô. Merchant chỉ thấy lượt của đơn thuộc tiệm mình (locker dùng chung). Khoảng ngày sai hoặc boxId không thuộc locker → 400.")
                .WithOpenApi();

            group.MapPost("/", async (CreateLockerCommand command, ILockerAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Thêm mới tủ Locker")
                .WithSummary("Thêm mới tủ Locker. Mọi Merchant phụ trách phải có bán kính phục vụ bao gồm tọa độ locker (Haversine, chưa làm tròn). Lỗi 400 meta.code: MerchantOutsideServiceRadius (meta.merchants: merchantId, businessName, distanceKm, serviceRadiusKm, blockedReasonCode), MerchantLocationUnavailable, LockerLocationRequired, NoEligibleMerchantForLocker, MerchantNotEligible. Từ chối cả yêu cầu, không ghi một phần.")
                .WithOpenApi();

            group.MapPut("/", async (UpdateLockerCommand command, ILockerAppService svc) =>
                (await svc.UpdateAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Cập nhật tủ Locker")
                .WithSummary("Cập nhật tủ Locker. Khi gửi merchantIds hoặc đổi tọa độ (kể cả merchantIds = null giữ danh sách), kiểm tra lại bán kính phục vụ của mọi Merchant sau thay đổi; cùng mã lỗi như khi thêm. Merchant mới thêm còn cần tài khoản hoạt động và hợp đồng hiệu lực.")
                .WithOpenApi();

            group.MapPut("/box-status", async (UpdateLockerBoxStatusCommand command, ILockerAppService svc) =>
                (await svc.UpdateBoxStatusAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Cập nhật trạng thái ô tủ Locker")
                .WithSummary("Cập nhật trạng thái ô tủ Locker.")
                .WithOpenApi();

            group.MapPost("/batch", async (AdministrativeBatchCommand command, ILockerAppService svc) => (await svc.BatchAsync(command)).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("1–50 locker: action=Delete|Status, status đích, id/expectedVersion từng dòng; operationId bắt buộc và replay.").WithOpenApi();
            group.MapPut("/{id:guid}/status", async (Guid id, SetLockerStatusCommand command, ILockerAppService svc) => (await svc.SetStatusAsync(id, command)).CustomResult())
                .RequireAuthorization("AdminOnly").WithSummary("Đổi trạng thái locker với expectedVersion và operationId; chặn ngừng hoạt động khi còn giữ ô/đồ.").WithOpenApi();
            group.MapDelete("/{id:guid}", async (Guid id, string? expectedVersion, Guid? operationId, ILockerAppService svc) =>
                (await svc.DeleteAsync(id, expectedVersion, operationId)).CustomResult())
                .RequireAuthorization("AdminOnly")
                .WithName("Xóa Locker")
                .WithSummary("Xóa mềm locker và các ô tủ; từ chối khi locker còn đơn hàng chưa xử lý.")
                .WithOpenApi();

            group.MapPost("/column-distinct-values", async (string field, LockerFilterQuery query, ILockerAppService svc) =>
                (await svc.GetColumnDistinctValuesAsync(field, query)).CustomResult())
                .WithName("Lấy giá trị distinct cho bộ lọc Locker")
                .WithSummary("Lấy giá trị distinct cho bộ lọc Locker.")
                .WithOpenApi();
        }
    }
}
