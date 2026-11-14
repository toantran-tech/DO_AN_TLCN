using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using WashGo.Application.Contracts.Features.CustomerContracts.Commands;
using WashGo.Application.Contracts.Features.CustomerContracts.Queries;
using WashGo.Application.Contracts.Features.CustomerContracts.Services;
using WashGo.Core.Endpoint;

namespace WashGo.HttpApi.Endpoints.Customer;

public class CustomerHandler : IEndpointBase
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customer").WithTags("Customer - Hồ sơ khách hàng")
            .RequireAuthorization("CustomerOnly");

        group.MapGet("/reviews", async (HttpRequest request, int? page, int? take, int? stars, ICustomerReviewAppService reviews) =>
        {
            if (request.Query.Keys.Any(key => key is not ("page" or "take" or "stars")))
                return Results.BadRequest(new { succeeded = false, message = "Chỉ hỗ trợ page, take, stars.", statusCode = 400 });
            return (await reviews.GetAsync(page ?? 1, take ?? 4, stars)).CustomResult();
        }).WithName("Customer xem feedback WashGo đã phát hành")
            .WithSummary("Feedback ẩn danh đã được Admin duyệt riêng cho Customer, không Hidden; page từ 1, take 1–20 (mặc định 4), stars 1–5 tùy chọn. Mới phát hành trước rồi public ID. Không hỗ trợ serviceId.").WithOpenApi();

        group.MapGet("/reviews/summary", async (HttpRequest request, ICustomerReviewAppService reviews) =>
        {
            if (request.Query.Count != 0)
                return Results.BadRequest(new { succeeded = false, message = "Summary tính toàn tập, không nhận bộ lọc.", statusCode = 400 });
            return (await reviews.SummaryAsync()).CustomResult();
        })
            .WithName("Customer xem tổng hợp feedback WashGo")
            .WithSummary("Tổng/điểm trung bình và phân bố đủ 1–5 sao của toàn tập được phát hành; averageStars=null khi rỗng, rate là % 0–100.").WithOpenApi();

        group.MapGet("/reviews/{publicId:guid}/images/{index:int}", async (Guid publicId, int index, HttpResponse response, ICustomerReviewAppService reviews) =>
        {
            response.Headers.CacheControl = "no-store";
            response.Headers["X-Content-Type-Options"] = "nosniff";
            var result = await reviews.GetImageAsync(publicId, index);
            return result.Succeeded && result.Data != null ? Results.File(result.Data.Bytes, result.Data.ContentType) : result.CustomResult();
        }).WithName("Customer xem ảnh feedback đã phát hành")
            .WithSummary("URL ảnh ẩn danh, không lộ owner/upload path. Cần JWT Customer (FE fetch blob), kiểm tra lại trạng thái duyệt/Hide mỗi request; index từ 0, không redirect sang URL gốc.").WithOpenApi();

        group.MapGet("/me", async (ICustomerAppService service) =>
            (await service.GetMyProfileAsync()).CustomResult())
            .WithName("Xem hồ sơ Customer của tôi")
            .WithSummary("Xem hồ sơ Customer của tôi.")
            .WithOpenApi();

        group.MapPut("/me", async (UpdateCustomerProfileCommand command, ICustomerAppService service) =>
            (await service.UpdateMyProfileAsync(command)).CustomResult())
            .WithName("Cập nhật hồ sơ Customer của tôi")
            .WithSummary("Cập nhật hồ sơ: bắt buộc họ tên, số điện thoại 10 số bắt đầu bằng 0, địa chỉ (tối đa 500 ký tự), tỉnh/thành phố (tối đa 100 ký tự).")
            .WithOpenApi();

        group.MapPost("/services/filter", async (CustomerServiceFilterQuery query, ICustomerCatalogAppService catalog) =>
            (await catalog.GetServicesAsync(query)).CustomResult())
            .WithName("Customer lọc và sắp xếp dịch vụ")
            .WithSummary("Lọc dịch vụ: serviceTypes là mảng loại dịch vụ; priceRanges là mảng khoảng minPrice/maxPrice, bao gồm hai đầu mút. OR trong từng tiêu chí, AND giữa loại/giá/keyword. Mảng rỗng = không lọc; page từ 1, sortBy Field=Name|UnitPrice và IsDescending.")
            .WithOpenApi();

        group.MapGet("/services/{id:guid}", async (Guid id, ICustomerCatalogAppService catalog) =>
            (await catalog.GetServiceAsync(id)).CustomResult())
            .WithName("Customer xem chi tiết dịch vụ đang bán")
            .WithSummary("Customer xem chi tiết dịch vụ đang bán.")
            .WithOpenApi();

        group.MapGet("/lockers", async (string? city, string? district, string? keyword,
            int? page, int? take, double? latitude, double? longitude, ICustomerCatalogAppService catalog) =>
            (await catalog.GetLockersAsync(city, district, keyword, page ?? 1, take ?? 20, latitude, longitude)).CustomResult())
            .WithName("Customer tìm tủ còn ô trống")
            .WithSummary("Customer tìm tủ còn ô trống; gửi latitude/longitude của khách để nhận khoảng cách (km) và sắp xếp gần nhất trước.")
            .WithOpenApi();

        group.MapGet("/lockers/{id:guid}", async (Guid id, ICustomerCatalogAppService catalog) =>
            (await catalog.GetLockerAsync(id)).CustomResult())
            .WithName("Customer xem chi tiết tủ và trạng thái ô")
            .WithSummary("Một locker theo id: slots gồm mọi ô chưa xóa theo slotNumber, status 1=Available/2=Occupied/3=Reserved/4=Maintenance; availableSlots chỉ gồm ô trống. Tủ hợp lệ hết ô trống vẫn trả 200. Khi tạo đơn, server kiểm tra và giữ ô lại atomically.")
            .WithOpenApi();
    }
}
