using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.AccountContracts.Commands;
using WashGo.Application.Contracts.Features.AccountContracts.Queries;
using WashGo.Application.Contracts.Features.AccountContracts.Services;
using WashGo.Application.Contracts.Features.AuditContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Admin
{
    /// <summary>Trang Admin quản lý tài khoản (Customer, Merchant, Shipper, Admin). Không có xóa cứng.</summary>
    public class AdminAccountHandler : IEndpointBase
    {
        /// <summary>If-Match: "12" / 12 / W/"12" → 12, used when the body has no expectedRevision.</summary>
        private static long? IfMatch(HttpContext http)
        {
            var raw = http.Request.Headers.IfMatch.ToString().Trim();
            if (raw.StartsWith("W/", StringComparison.Ordinal)) raw = raw[2..];
            raw = raw.Trim('"');
            return long.TryParse(raw, out var value) ? value : null;
        }

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/admin/accounts")
                .WithTags("Admin - Quản lý tài khoản")
                .RequireAuthorization("AdminOnly");

            group.MapGet("/", async ([AsParameters] AccountListQuery query, IAdminAccountAppService svc) =>
                    (await svc.GetListAsync(query)).CustomResult())
                .WithName("Danh sách tài khoản")
                .WithSummary("Phân trang server (page, take ≤ 100, total, hasMore). Tìm họ tên/email/SĐT/tên cửa hàng; lọc role, isActive, createdFrom/createdTo (unix giây). Mặc định không gồm tài khoản đã xóa; deleted=Include|Only cần Accounts.ReadDeleted. sortBy createdAt|lastLoginAt|fullName|deletedAt, khóa phụ id; lastLoginAt null luôn ở cuối. Mỗi dòng có revision, avatarUrl (null = mặc định), partner, capabilities (không tính nghĩa vụ).")
                .WithOpenApi();

            group.MapGet("/summary", async (IAdminAccountAppService svc) => (await svc.GetSummaryAsync()).CustomResult())
                .WithName("Tổng hợp tài khoản")
                .WithSummary("Tổng/đang hoạt động/bị khóa theo role trên tài khoản chưa xóa, tính bằng một truy vấn; deleted chỉ trả khi có Accounts.ReadDeleted.")
                .WithOpenApi();

            group.MapGet("/operations/{operationId:guid}", async (Guid operationId, IAdminAccountAppService svc) =>
                    (await svc.GetOperationAsync(operationId)).CustomResult())
                .WithName("Tra cứu thao tác tài khoản")
                .WithSummary("Kết quả của một thao tác (đơn lẻ, hàng loạt, tạo Customer/Admin/Merchant/Shipper) do chính bạn gửi, dùng khi timeout/mất phản hồi. state Processing = đang chạy (gửi lại cùng operationId và payload là an toàn); Completed kèm result (data.id = account ID). 404 = chưa có kết quả của bạn, không chứng minh thao tác đã thất bại. Không tạo lại bằng khóa mới.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IAdminAccountAppService svc) => (await svc.GetAsync(id)).CustomResult())
                .WithName("Chi tiết tài khoản")
                .WithSummary("Thông tin tài khoản, người/lý do/thời điểm khóa-mở-xóa, quyền cấp riêng (Admin), nghĩa vụ còn mở (đơn, thanh toán, hoàn tiền, khiếu nại, đối soát, Shipper, locker, nhiệm vụ) và capabilities đầy đủ. Không trả hash/token/OTP/provider subject/giấy tờ.")
                .WithOpenApi();

            group.MapGet("/{id:guid}/activities", async (Guid id, [AsParameters] AccountActivityQuery query, IAdminAccountAppService svc) =>
                    (await svc.GetActivitiesAsync(id, query)).CustomResult())
                .WithName("Nhật ký tài khoản")
                .WithSummary("Audit phân trang của tài khoản (trang tài khoản, đăng nhập, trang Merchant/Shipper), mới nhất trước.")
                .WithOpenApi();

            group.MapPost("/", async (CreateAccountCommand command, IAdminAccountAppService svc) =>
                    (await svc.CreateAsync(command)).CustomResult())
                .WithName("Tạo tài khoản")
                .WithSummary("role = Customer|Admin: fullName, email, phone, avatarUrl (tùy chọn); mật khẩu tạm gửi qua email sau commit (credentialDelivery). Admin cần Accounts.ManageAdmins + xác thực lại. role = Merchant|Shipper: gửi merchant/shipper với payload như POST /merchant, POST /shipper. operationId chống tạo trùng. Email trùng (không phân biệt hoa thường, kể cả tài khoản bị khóa) → 409 EmailAlreadyInUse.")
                .WithOpenApi();

            group.MapPatch("/{id:guid}", async (Guid id, UpdateAccountCommand command, HttpContext http, IAdminAccountAppService svc) =>
                {
                    command.ExpectedRevision ??= IfMatch(http);
                    return (await svc.UpdateAsync(id, command)).CustomResult();
                })
                .WithName("Sửa tài khoản")
                .WithSummary("Sửa fullName, phone, avatarUrl (\"\" = xóa). Cần expectedRevision hoặc If-Match. Không đổi role/email/mật khẩu/trạng thái ở đây (email: /email-change). Lỗi trả đúng trường trong errors.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/status", async (Guid id, SetAccountStatusCommand command, HttpContext http, IAdminAccountAppService svc) =>
                {
                    command.ExpectedRevision ??= IfMatch(http);
                    return (await svc.SetStatusAsync(id, command)).CustomResult();
                })
                .WithName("Khóa/mở tài khoản")
                .WithSummary("isActive đích rõ ràng. Khóa cần reason; thu hồi mọi phiên, chặn access token ở request kế tiếp, đóng kết nối thời gian thực. Customer/Merchant còn đơn → 409 ConfirmationRequired (gửi confirm=true); Shipper còn nhiệm vụ → 409 HasActiveTasks. Mở không khôi phục phiên, không tự duyệt Merchant/đổi trạng thái nhận việc. Đã ở trạng thái đích → 200 meta.outcome=Unchanged.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/soft-delete", async (Guid id, SoftDeleteAccountCommand command, HttpContext http, IAdminAccountAppService svc) =>
                {
                    command.ExpectedRevision ??= IfMatch(http);
                    return (await svc.SoftDeleteAsync(id, command)).CustomResult();
                })
                .WithName("Xóa mềm tài khoản")
                .WithSummary("Không bao giờ xóa cứng. Cần reason, expectedRevision. Còn nghĩa vụ → 409 AccountHasOpenObligations (meta.blockers, data.obligations). deletedAt là giờ server; xóa lại → 200 Unchanged, không đổi deletedAt. Email/Google được giải phóng cho tài khoản mới.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/restore", async (Guid id, RestoreAccountCommand command, HttpContext http, IAdminAccountAppService svc) =>
                {
                    command.ExpectedRevision ??= IfMatch(http);
                    return (await svc.RestoreAsync(id, command)).CustomResult();
                })
                .WithName("Khôi phục tài khoản")
                .WithSummary("Cần Accounts.Restore; chỉ Customer/Admin. Email/Google đã có tài khoản mới dùng → 409 IdentityAlreadyInUse (không tự gộp/đổi email). Khôi phục về trạng thái khóa, không khôi phục phiên.")
                .WithOpenApi();

            group.MapPut("/{id:guid}/permissions", async (Guid id, UpdateAccountPermissionsCommand command, HttpContext http, IAdminAccountAppService svc) =>
                {
                    command.ExpectedRevision ??= IfMatch(http);
                    return (await svc.UpdatePermissionsAsync(id, command)).CustomResult();
                })
                .WithName("Cấp quyền quản lý tài khoản")
                .WithSummary("Đặt toàn bộ quyền cấp riêng của một Admin: Accounts.ManageAdmins, Accounts.ReadDeleted, Accounts.Restore. Cần Accounts.ManageAdmins + xác thực lại + reason; không tự đổi quyền của mình; luôn giữ ít nhất một Admin hoạt động có ManageAdmins.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/email-change", async (Guid id, RequestAccountEmailChangeCommand command, IAdminAccountAppService svc) =>
                    (await svc.RequestEmailChangeAsync(id, command)).CustomResult())
                .WithName("Yêu cầu đổi email")
                .WithSummary("Gửi OTP (10 phút) tới email mới để chứng minh quyền sở hữu; email đã dùng → 409; gửi lại sau 60 giây.")
                .WithOpenApi();

            group.MapPost("/{id:guid}/email-change/confirm", async (Guid id, ConfirmAccountEmailChangeCommand command, HttpContext http, IAdminAccountAppService svc) =>
                {
                    command.ExpectedRevision ??= IfMatch(http);
                    return (await svc.ConfirmEmailChangeAsync(id, command)).CustomResult();
                })
                .WithName("Xác nhận đổi email")
                .WithSummary("Nhập mã OTP chủ email mới nhận được; đổi email đăng nhập. Liên kết Google giữ nguyên theo tài khoản Google, không chuyển theo email.")
                .WithOpenApi();

            group.MapPost("/bulk/preview", async (AccountBulkPreviewCommand command, IAdminAccountAppService svc) =>
                    (await svc.PreviewBulkAsync(command)).CustomResult())
                .WithName("Xem trước thao tác hàng loạt")
                .WithSummary("action Lock|Unlock|SoftDelete, 1–100 ID. Mỗi dòng: thông tin tối thiểu, canApply, reasonCodes, requiresConfirmation, nghĩa vụ. Không giữ chỗ hay bảo đảm thành công.")
                .WithOpenApi();

            group.MapPost("/bulk", async (AccountBulkCommand command, IAdminAccountAppService svc) =>
                    (await svc.BulkAsync(command)).CustomResult())
                .WithName("Thao tác hàng loạt")
                .WithSummary("operationId bắt buộc; action Lock|Unlock|SoftDelete; items [{id, expectedRevision}] ≤ 100; reason; confirm. Mỗi tài khoản một transaction, kết quả từng dòng (Changed/Unchanged/Skipped/Failed + reasonCode + version mới). Gửi lại cùng khóa trả cùng kết quả; khóa cũ khác payload → 409. Không đổi role hàng loạt.")
                .WithOpenApi();
        }
    }
}
