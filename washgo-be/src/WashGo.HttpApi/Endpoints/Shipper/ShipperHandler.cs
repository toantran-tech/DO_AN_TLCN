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
                .WithSummary("Merchant xem Shipper của tiệm (họ tên, SĐT, phương tiện, biển số, trạng thái, số đơn đã giao, đánh giá); Admin lọc được theo Merchant.")
                .WithOpenApi();

            group.MapGet("/{id:guid}", async (Guid id, IShipperManagementAppService svc) =>
                (await svc.GetByIdAsync(id)).CustomResult())
                .WithName("Xem chi tiết Shipper")
                .WithSummary("Xem chi tiết Shipper thuộc tiệm của mình.")
                .WithOpenApi();

            group.MapPost("/", async (CreateShipperCommand command, IShipperManagementAppService svc) =>
                (await svc.CreateAsync(command)).CustomResult())
                .WithName("Thêm mới Shipper")
                .WithSummary("Tạo tài khoản role Shipper cho tiệm; mật khẩu tạm được gửi qua email (không trả về trong API).")
                .WithOpenApi();

            group.MapPut("/{id:guid}", async (Guid id, UpdateShipperCommand command, IShipperManagementAppService svc) =>
                (await svc.UpdateAsync(id, command)).CustomResult())
                .WithName("Chỉnh sửa Shipper")
                .WithSummary("Cập nhật họ tên, SĐT, email, phương tiện, biển số và trạng thái Đang làm việc/Tạm nghỉ.")
                .WithOpenApi();
        }
    }
}
