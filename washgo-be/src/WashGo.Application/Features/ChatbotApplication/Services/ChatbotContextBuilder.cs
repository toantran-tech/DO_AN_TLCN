using System.Text.Json.Nodes;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using WashGo.Application.Contracts.Features.ChatbotContracts.Commands;
using WashGo.Application.Contracts.Features.ChatbotContracts.Results;
using WashGo.Application.Contracts.Features.CustomerContracts.Queries;
using WashGo.Application.Contracts.Features.CustomerContracts.Services;
using WashGo.Application.Contracts.Features.PolicyContracts.Services;
using WashGo.Application.Contracts.Features.WashOrderContracts.Services;
using WashGo.Application.Features.ChatbotApplication.Knowledge;
using WashGo.Application.Features.PaymentApplication.Services;
using WashGo.Application.Features.SystemSettingApplication.Services;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Entities.WashOrder.Interfaces;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.ChatbotApplication.Services;

public class ChatbotContextBuilder(ICustomerCatalogAppService catalog, IPolicyAppService policy, IWashOrderAppService orderService,
    IWashOrderRepository orders, SystemSettingProvider settings, IAsyncQueryableExecuter query) : ITransientDependency
{
    public static string? Validate(ChatContextCommand? context)
    {
        if (context?.SelectedOrderId == Guid.Empty) return "SelectedOrderId không hợp lệ.";
        if (context?.City?.Length > 100 || context?.District?.Length > 100) return "Khu vực tối đa 100 ký tự.";
        if (context?.Location is { } location && (location.Latitude is not { } lat || location.Longitude is not { } lng ||
            !double.IsFinite(lat) || !double.IsFinite(lng) || lat is < -90 or > 90 || lng is < -180 or > 180))
            return "Cần đủ latitude/longitude hợp lệ và sự đồng ý chia sẻ vị trí.";
        return null;
    }

    public async Task<ChatToolResult> ExecuteAsync(Guid owner, string name, JsonObject args, ChatContextCommand? context)
    {
        var cards = new List<ChatCardDto>();
        var sources = new List<ChatSourceDto> { Source("washgo-guide", "Hướng dẫn WashGo", ChatbotKnowledge.Version) };
        object data;
        switch (name)
        {
            case "guide":
                Allowed(args);
                data = new { instruction = "Trả lời trong phạm vi hướng dẫn. Thiếu thông tin thì hỏi lại, ngoài phạm vi thì từ chối nhẹ nhàng." };
                break;
            case "services":
                Allowed(args, "keyword", "minPrice", "maxPrice", "serviceTypes");
                var types = args["serviceTypes"]?.AsArray().Select(x => (WashServiceType)x!.GetValue<int>()).ToArray() ?? [];
                if (types.Length > 12 || types.Any(x => !Enum.IsDefined(x))) throw new ChatbotException("InvalidAiToolCall");
                var min = args["minPrice"]?.GetValue<decimal>(); var max = args["maxPrice"]?.GetValue<decimal>();
                if (min < 0 || max < 0 || min > max || min > 1_000_000_000 || max > 1_000_000_000) throw new ChatbotException("InvalidAiToolCall");
                var found = await catalog.GetServicesAsync(new CustomerServiceFilterQuery
                {
                    Keyword = Arg(args, "keyword"), Take = 5, Page = 1, ServiceTypes = types,
                    PriceRanges = min.HasValue || max.HasValue ? [new ServicePriceRangeQuery { MinPrice = min, MaxPrice = max }] : []
                });
                if (!found.Succeeded) throw new ChatbotException("ChatDataUnavailable");
                foreach (var service in found.List)
                    cards.Add(Card("Service", service.Id, service.Name, new { service.Code, service.Name, description = Short(service.Description, 500),
                        service.Unit, service.UnitPrice, service.PricePerKg, service.PricePerItem, service.EstimatedDurationHours, service.ServiceType }));
                sources.Add(Source("service-catalog", "Danh mục dịch vụ đang bán"));
                data = new { services = cards, noResults = cards.Count == 0, note = "Giá niêm yết, không phải giá cuối của đơn sau cân/giảm giá/phụ phí. Tối đa 5 kết quả." };
                break;
            case "lockers":
                Allowed(args, "city", "district", "keyword");
                var city = context?.City?.Trim() ?? Arg(args, "city"); var district = context?.District?.Trim() ?? Arg(args, "district");
                if (context?.Location == null && string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(district) && string.IsNullOrWhiteSpace(Arg(args, "keyword")))
                {
                    data = new { requiresLocationOrArea = true, instruction = "Xin phép chia sẻ vị trí hoặc hỏi tỉnh/thành phố, quận/huyện. Chưa có vị trí nên không biết locker gần nhất." };
                    break;
                }
                var lockers = await catalog.GetLockersAsync(city, district, Arg(args, "keyword"), 1, 5, context?.Location?.Latitude, context?.Location?.Longitude);
                if (!lockers.Succeeded) throw new ChatbotException("ChatDataUnavailable");
                foreach (var locker in lockers.List)
                    cards.Add(Card("Locker", locker.Id, locker.Name, new { locker.Code, locker.Name, locker.Address, locker.Ward, locker.District,
                        locker.City, locker.DistanceKm, locker.AvailableSlotCount }));
                sources.Add(Source("locker-catalog", "Locker hoạt động còn ô trống"));
                data = new { lockers = cards, noResults = cards.Count == 0, hasPosition = context?.Location != null,
                    note = "Khoảng cách đường chim bay; số ô tại lúc tra cứu, chưa giữ chỗ. Không có tọa độ thì chỉ là kết quả theo khu vực, không khẳng định gần nhất." };
                break;
            case "order":
                Allowed(args, "orderCode");
                var orderId = await ResolveOrderAsync(owner, context?.SelectedOrderId, Arg(args, "orderCode"));
                if (orderId == null)
                {
                    // Do not implicitly select one of the customer's orders. Let them choose explicitly.
                    data = new { instruction = "Không tìm thấy đơn được chọn thuộc khách. Yêu cầu chọn đơn hoặc cung cấp mã đơn; không tiết lộ đơn người khác." };
                    break;
                }
                var detail = await orderService.GetByIdAsync(orderId.Value);
                if (!detail.Succeeded || detail.Data == null) throw new ChatbotException("ChatDataUnavailable");
                var order = detail.Data;
                var rawOrder = await orders.FindAsync(orderId.Value);
                if (rawOrder == null || rawOrder.CustomerId != owner) throw new ChatbotException("ChatDataUnavailable");
                var applied = await settings.ForOrderAsync(rawOrder);
                var storageDue = OverdueFeeCalculator.GetTotalStorageDue(rawOrder, DateTime.UtcNow, applied);
                // Explicit allowlist: never serialize the detail DTO itself (it includes customer access codes).
                cards.Add(Card("Order", order.Id, order.OrderCode, new { order.OrderCode, status = order.Status.ToString(), paymentStatus = order.PaymentStatus.ToString(),
                    order.TotalAmount, order.FinalAmount, order.DiscountAmount, order.WeightSurchargeAmount, order.ActualWeightKg,
                    order.PredictedFinishAt, order.PredictionSource, order.PredictionStatus, order.PredictionUpdatedAt,
                    order.DepositDeadline, order.PickupDeadline, order.PaymentDeadline, order.MovedToStorageAt, order.CancelFeeAmount,
                    order.CancellationFeeOutstanding, order.CancellationRefundAmount, totalStorageDue = storageDue,
                    storagePaid = order.StorageFeeTotal, storageOutstanding = Math.Max(0, storageDue - order.StorageFeeTotal),
                    nextStep = NextStep(rawOrder, storageDue),
                    etaMeaning = "Giặt xong/chờ thanh toán; không phải lúc đồ về locker", observedAt = DateTime.UtcNow }));
                var quote = await orderService.GetCancellationQuoteAsync(order.Id);
                sources.Add(Source("my-order", "Chi tiết và báo phí đơn của bạn", rawOrder.OperationalPolicyBasis));
                data = new { order = cards[0], cancellationQuote = quote.Succeeded ? quote.Data : null,
                    instruction = "Chỉ giải thích số server trả. Báo phí hủy là tại lúc tra cứu, kiểm tra lại khi khách xác nhận. Không tự gọi thanh toán/hủy hoặc bịa nguyên nhân trễ." };
                break;
            case "policy":
                Allowed(args);
                var current = await policy.GetCurrentAsync();
                if (!current.Succeeded || current.Data == null) throw new ChatbotException("ChatDataUnavailable");
                var live = current.Data;
                object? snapshot = null;
                if (context?.SelectedOrderId is { } selected)
                {
                    var selectedId = await ResolveOrderAsync(owner, selected, null);
                    if (selectedId == null) throw new ChatbotException("ChatOrderNotFound");
                    var own = await orders.GetAsync(selectedId.Value);
                    var rules = await settings.ForOrderAsync(own);
                    snapshot = new { own.OrderCode, own.OperationalPolicyBasis, own.OperationalPolicyCapturedAt, rules.DepositWindowMinutes,
                        rules.FreePickupHours, rules.CancelLatePendingPercent, rules.CancelDepositedPercent, rules.CancelCollectedPercent,
                        rules.OverdueFeeDay1, rules.OverdueFeeDay2, rules.OverdueFeeDay3, rules.StorageFeePerDay };
                }
                sources.Add(Source("terms-current", live.Terms.Title, live.Terms.Version));
                var policyData = new { live.Terms.Id, live.Terms.Version, live.Terms.EffectiveFrom,
                    content = Short(live.Terms.Content, 6000), contentTruncated = live.Terms.Content.Length > 6000, live.Rules, orderOperationalSnapshot = snapshot,
                    instruction = "Rules là cấu hình hiện tại; nếu có snapshot đơn thì dùng snapshot cho phí/hạn của đơn đó. Bồi thường do Admin duyệt theo chính sách lúc xử lý, không tự hứa. Nội dung bị cắt thì không khẳng định đã đầy đủ điều khoản." };
                cards.Add(Card("Policy", live.Terms.Id, live.Terms.Title, policyData));
                data = policyData;
                break;
            default: throw new ChatbotException("InvalidAiToolCall");
        }
        return new(ChatbotJson.Element(new { knowledge = ChatbotKnowledge.Content, currentData = data }), sources, cards);
    }

    private async Task<Guid?> ResolveOrderAsync(Guid owner, Guid? id, string? code)
    {
        if (!id.HasValue && string.IsNullOrWhiteSpace(code)) return null;
        var q = (await orders.GetQueryableAsync()).Where(x => !x.IsDeleted && x.CustomerId == owner);
        q = id.HasValue ? q.Where(x => x.Id == id) : q.Where(x => x.OrderCode == code);
        return await query.FirstOrDefaultAsync(q.Select(x => (Guid?)x.Id));
    }
    // Guidance only. Actual action endpoints always revalidate state, ownership, quotes and codes.
    private static string NextStep(WashOrder order, decimal storageDue) => order.Status switch
    {
        WashOrderStatus.Pending when order.DepositDeadline < DateTime.UtcNow => "Xem báo phí và hủy đơn đã quá hạn gửi đồ.",
        WashOrderStatus.Pending => "Mở chi tiết đơn để gửi đồ vào locker trong thời hạn.",
        WashOrderStatus.AwaitingPayment => "Xem giá đã chốt và thanh toán trực tuyến sau khi giặt xong.",
        WashOrderStatus.ReadyToReturn when order.StorageFeeTotal < storageDue => "Thanh toán phí quá hạn còn thiếu trước khi nhận đồ.",
        WashOrderStatus.ReadyToReturn => "Mở chi tiết đơn, xác thực mở ô, lấy hết đồ rồi xác nhận đóng cửa.",
        WashOrderStatus.TransferringToStorage => "Đồ đang được chuyển về khu lưu tại tiệm; chưa xác nhận vào kho. Liên hệ WashGo nếu cần hỗ trợ.",
        WashOrderStatus.ReturningFromStorage => "Đồ đang được giao lại tới locker đã chọn; chờ thông báo đã đặt vào ô, chưa có mã nhận mới.",
        WashOrderStatus.MovedToStorage => "Kiểm tra tiền giặt/phí kho, lấy báo phí và yêu cầu giao lại locker trên WashGo; không tự đến tiệm nhận đồ.",
        WashOrderStatus.Cancelled when order.CancelFeePaidAmount < order.CancelFeeAmount => "Thanh toán phí hủy còn thiếu trước khi nhận lại đồ.",
        WashOrderStatus.Cancelled when order.CancelledGoodsStatus is CancelledGoodsStatus.AtDepositLocker or CancelledGoodsStatus.AtReturnLocker => "Mở chi tiết đơn để xác thực và nhận lại đồ hủy tại locker.",
        WashOrderStatus.Cancelled when order.CancelledGoodsStatus == CancelledGoodsStatus.AtMerchant => "Chờ phân công Shipper trả lại đồ hủy về locker.",
        WashOrderStatus.Cancelled => "Xem chi tiết hủy và trạng thái yêu cầu hoàn tiền nếu có.",
        WashOrderStatus.Completed => "Xem biên lai, đánh giá hoặc báo sự cố nếu còn trong điều kiện cho phép.",
        _ => "Theo dõi tiến độ trong chi tiết đơn; chưa cần thực hiện thanh toán hoặc nhận đồ khi chưa đến bước."
    };
    private static ChatCardDto Card(string type, Guid id, string title, object value) => new() { Type = type, Id = id, Title = title, Data = ChatbotJson.Element(value) };
    private static ChatSourceDto Source(string code, string title, string? version = null) => new() { Code = code, Title = title, Version = version, RetrievedAt = DateTime.UtcNow };
    private static string? Short(string? value, int count) => value?[..Math.Min(value.Length, count)];
    private static string? Arg(JsonObject args, string key)
    {
        if (args[key] == null) return null;
        var value = args[key]!.GetValue<string>().Trim();
        if (value.Length > 100 || value.Any(char.IsControl)) throw new ChatbotException("InvalidAiToolCall");
        return value.Length == 0 ? null : value;
    }
    private static void Allowed(JsonObject args, params string[] keys)
    {
        if (args.Any(x => !keys.Contains(x.Key))) throw new ChatbotException("InvalidAiToolCall");
    }
}
