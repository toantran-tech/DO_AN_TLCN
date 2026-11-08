using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.AdminContracts.Commands;
using WashGo.Application.Contracts.Features.AdminContracts.Results;
using WashGo.Application.Contracts.Features.AdminContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Admin
{
    public class AdminProfileHandler : IEndpointBase
    {
        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/admin")
                .WithTags("Admin - Hồ sơ cá nhân")
                .RequireAuthorization("AdminOnly");

            group.MapGet("/profile", async (IAdminProfileAppService svc) => (await svc.GetAsync()).CustomResult())
                .WithName("Xem hồ sơ Admin")
                .WithSummary("Hồ sơ của chính Admin đang đăng nhập: email, họ tên, SĐT, ảnh, vai trò, ngày tham gia (UTC), trạng thái tài khoản, lần đăng nhập gần nhất, version và danh sách trường được sửa.")
                .WithOpenApi();

            group.MapPut("/profile", async (UpdateAdminProfileCommand command, IAdminProfileAppService svc) =>
                    (await svc.UpdateAsync(command)).CustomResult())
                .WithName("Cập nhật hồ sơ Admin")
                .WithSummary("Chỉ sửa họ tên (2-100), SĐT, ảnh đại diện (ảnh tự upload; \"\" = xóa; null = giữ). Bắt buộc expectedVersion; lệch → 409 kèm hồ sơ mới nhất. Gửi lại cùng nội dung sau lỗi mạng an toàn (không có thay đổi). Email/vai trò/trạng thái không sửa ở đây.")
                .WithOpenApi();
        }
    }
}
