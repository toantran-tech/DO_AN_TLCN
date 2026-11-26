using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using WashGo.Application.Contracts.Features.WashOrderContracts.Commands;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Core.Domain.Shared.Bases;
using WashGo.Domain.Entities.WashOrder;
using WashGo.Domain.Shared.Enums;
using PromotionEntity = WashGo.Domain.Entities.Promotion.Promotion;

namespace WashGo.Application.Features.WashOrderApplication.Services;

public partial class WashOrderAppService
{
    private static string NormalizeOrderUnit(string unit) => unit.Trim().ToLowerInvariant() switch
    {
        "kg" => "kg",
        "đôi" or "doi" or "pair" or "pairs" => "pair",
        _ => "item"
    };

    public async Task<BaseResponse<OrderActualsQuoteDto>> PreviewActualsAsync(Guid orderId, ConfirmOrderActualsCommand command)
    {
        var scope = await GetOrderScopeAsync();
        var order = await _orderRepo.GetWithDetailsAsync(orderId);
        if (order == null || scope.Role != "Merchant" || !CanAccess(order, scope))
            return NotFound<OrderActualsQuoteDto>("Không tìm thấy đơn hàng thuộc tiệm.");

        var (quote, error) = CalculateActuals(order, scope.Role, command);
        return quote == null ? ActualsFailure<OrderActualsQuoteDto>(error!) : Success(quote);
    }

    public async Task<BaseResponse<WashOrderDetailDto>> ConfirmActualsAsync(Guid orderId, ConfirmOrderActualsCommand command)
    {
        if (!command.ExpectedRevision.HasValue || command.OperationId is null || command.OperationId == Guid.Empty ||
            !command.ExpectedFinalAmount.HasValue)
            return BadRequest<WashOrderDetailDto>("Cần gửi expectedRevision, operationId và expectedFinalAmount từ báo giá.");

        var scope = await GetOrderScopeAsync();
        using var uow = UnitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        await OrderOperationLocks.AcquireAsync(_orderRepo, command.OperationId);
        await _orderRepo.GetForUpdateAsync(orderId);
        var order = await _orderRepo.GetWithDetailsAsync(orderId);
        if (order == null || scope.Role != "Merchant" || !CanAccess(order, scope))
            return NotFound<WashOrderDetailDto>("Không tìm thấy đơn hàng thuộc tiệm.");

        if (command.Items == null || command.Items.Any(item => item == null))
            return BadRequest<WashOrderDetailDto>("Danh sách kiểm đếm không hợp lệ.");
        var counts = string.Join(",", command.Items.OrderBy(item => item.OrderItemId)
            .Select(item => FormattableString.Invariant($"{item.OrderItemId:N}:{item.ActualQuantity}")));
        var request = OrderWriteGuard.Fingerprint(
            FormattableString.Invariant($"weight={command.ActualWeightKg};final={command.ExpectedFinalAmount}"),
            counts + "|" + command.Note?.Trim());
        const string operation = "ConfirmOrderActuals";
        if (await GuardAsync(order, scope, command, operation, request) is { } stopped)
            return stopped;

        var (quote, error) = CalculateActuals(order, scope.Role, command);
        if (quote == null)
            return ActualsFailure<WashOrderDetailDto>(error!);

        if (quote.FinalAmount != command.ExpectedFinalAmount)
            return new BaseResponse<WashOrderDetailDto>
            {
                StatusCode = HttpStatusCode.Conflict,
                Message = "Giá đã thay đổi. Vui lòng xem trước giá và xác nhận lại.",
                Meta = new OrderWriteMetaDto { Code = "ActualsQuoteChanged", CurrentRevision = order.Revision, CurrentStatus = order.Status }
            };

        var lines = quote.Items.ToDictionary(item => item.OrderItemId);
        foreach (var item in order.Items.Where(item => !item.IsWeightBased))
        {
            var confirmed = lines[item.Id];
            item.ActualQuantity = confirmed.ActualQuantity;
            item.ActualSubTotal = confirmed.Amount;
        }

        var now = DateTime.UtcNow;
        if (order.PromotionId.HasValue &&
            await _promotionUsageRepo.FirstOrDefaultAsync(usage => usage.OrderId == order.Id && !usage.IsReverted) is { } activeUsage)
        {
            if (quote.PromotionRemoved)
            {
                activeUsage.IsReverted = true;
                activeUsage.RevertedAt = now;
            }
            else
            {
                activeUsage.OrderAmount = quote.TotalAmount;
                activeUsage.DiscountAmount = quote.DiscountAmount;
            }
            await _promotionUsageRepo.UpdateAsync(activeUsage, autoSave: true);
            if (quote.PromotionRemoved)
                await _promotionRepo.TryReleaseAsync(activeUsage.PromotionId);
        }
        if (quote.PromotionRemoved)
        {
            order.PromotionId = null;
            order.PromotionCode = null;
        }

        order.ActualsConfirmedAt = now;
        order.ActualsConfirmedBy = CurrentUser.Id;
        order.ActualsNote = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
        order.ActualWeightKg = command.ActualWeightKg;
        order.TotalAmount = quote.TotalAmount;
        order.WeightSurchargeAmount = quote.WeightSurchargeAmount;
        order.DiscountAmount = quote.DiscountAmount;
        order.FinalAmount = quote.FinalAmount;
        if (order.BaseProcessingHours is { } baseHours)
        {
            var hours = command.ActualWeightKg is { } kg
                ? WashOrderWeightPricing.Calculate(kg, order.IncludedWeightKg.GetValueOrDefault(), baseHours).ProcessingHours
                : baseHours;
            order.EtaUpdatedAt = now;
            order.EstimatedFinishAt = now.AddHours(hours);
            order.ExpectedDeliveryTime = new DateTimeOffset(order.EstimatedFinishAt.Value).ToUnixTimeSeconds();
        }

        order.Timeline.Add(new WashOrderTimeline
        {
            WashOrderId = order.Id,
            EventType = "ActualsConfirmed",
            FromStatus = order.Status,
            Status = order.Status,
            ActorId = CurrentUser.Id,
            ActorName = CurrentUser.UserName ?? "Merchant",
            ActorRole = "Merchant",
            Timestamp = new DateTimeOffset(now).ToUnixTimeSeconds(),
            Note = order.ActualsNote ?? "Merchant đã xác nhận số lượng thực tế."
        });
        await _orderRepo.UpdateAsync(order, autoSave: true);
        await _writeGuard.RecordAsync(order, CurrentUser.Id!.Value, command, operation, request);
        await uow.CompleteAsync();

        var result = RedactStaffData(ObjectMapper.Map<WashOrder, WashOrderDetailDto>(order), scope);
        result.AllowedActions = OrderActionRules.Build(order, scope.Role);
        return Success(result, "Đã xác nhận số lượng thực tế và chốt giá.");
    }

    private sealed record ActualsError(string Code, string Message, Guid? OrderItemId = null);

    private BaseResponse<T> ActualsFailure<T>(ActualsError error)
    {
        var response = BadRequest<T>(error.Message);
        response.Meta = new { code = error.Code, orderItemId = error.OrderItemId };
        if (error.OrderItemId.HasValue)
            response.Errors[error.OrderItemId.Value.ToString()] = [error.Message];
        return response;
    }

    private static (OrderActualsQuoteDto? Quote, ActualsError? Error) CalculateActuals(
        WashOrder order, string role, ConfirmOrderActualsCommand command)
    {
        if (OrderActionRules.ConfirmActuals(order, role) is { } block)
            return (null, new(block.Code, block.Message));

        var countItems = order.Items.Where(item => !item.IsWeightBased).ToDictionary(item => item.Id);
        if (command.Items == null || command.Items.Count != countItems.Count)
            return (null, new("MissingActualsLines", "Cần xác nhận đủ mọi dòng dịch vụ theo cái hoặc đôi."));

        var note = command.Note?.Trim();
        if (note?.Length > 500)
            return (null, new("ActualsNoteTooLong", "Lý do chênh lệch có tối đa 500 ký tự."));

        var quote = new OrderActualsQuoteDto
        {
            Revision = order.Revision,
            Status = order.Status,
            PreviousFinalAmount = order.FinalAmount
        };
        foreach (var input in command.Items)
        {
            if (input == null || !countItems.Remove(input.OrderItemId, out var item))
                return (null, new("InvalidActualsLine", "Dòng dịch vụ bị trùng hoặc không thuộc đơn.", input?.OrderItemId));
            if (input.ActualQuantity is not int quantity || quantity is < 0 or > 1000)
                return (null, new("InvalidActualQuantity", "Cần số lượng nguyên từ 0 đến 1.000.", item.Id));
            if (quantity != item.Quantity && string.IsNullOrWhiteSpace(note))
                return (null, new("ActualsNoteRequired", "Vui lòng ghi lý do khi số lượng thực tế khác số lượng đã đặt.", item.Id));
            quote.Items.Add(new OrderActualsLineDto
            {
                OrderItemId = item.Id,
                Unit = item.UnitSnapshot,
                Quantity = item.Quantity,
                ActualQuantity = quantity,
                UnitPrice = item.UnitPrice,
                Amount = quantity * item.UnitPrice
            });
        }

        var weightItems = order.Items.Where(item => item.IsWeightBased).ToList();
        if (weightItems.Count > 0)
        {
            if (command.ActualWeightKg is not decimal kg || kg <= 0 || kg > 1000 || decimal.Round(kg, 2) != kg)
                return (null, new("InvalidActualWeight", "Cần cân thực tế từ trên 0 đến 1.000 kg, tối đa 2 chữ số thập phân."));
            if (order.IncludedWeightKg is not > 0 || order.BaseProcessingHours is not > 0)
                return (null, new("MissingWeightSnapshot", "Thiếu thông tin gói kg đã chốt lúc đặt đơn."));
            quote.WeightSurchargeAmount = WashOrderWeightPricing.Calculate(kg,
                order.IncludedWeightKg.Value, order.BaseProcessingHours.Value).SurchargeAmount;
        }
        else if (command.ActualWeightKg.HasValue)
            return (null, new("NoWeightService", "Đơn chỉ có dịch vụ theo cái/đôi, không nhập số kg."));

        quote.TotalAmount = weightItems.Sum(item => item.SubTotal) + quote.Items.Sum(item => item.Amount);
        if (quote.TotalAmount <= 0)
            return (null, new("EmptyActuals", "Đơn không còn dịch vụ tính phí. Vui lòng liên hệ hỗ trợ để xử lý ngoại lệ."));

        if (order.PromotionId.HasValue)
        {
            if (order.ActualsDiscountTypeSnapshot is not { } type || order.ActualsDiscountValueSnapshot is not { } value ||
                order.ActualsMinOrderAmountSnapshot is not { } minimum)
                return (null, new("MissingPromotionSnapshot", "Thiếu điều khoản voucher tại lúc đặt. Vui lòng liên hệ hỗ trợ."));
            var policy = new PromotionEntity { DiscountType = type, DiscountValue = value, MaxDiscountAmount = order.ActualsMaxDiscountAmountSnapshot };
            quote.DiscountAmount = quote.TotalAmount >= minimum ? policy.CalculateDiscount(quote.TotalAmount) : 0;
            quote.PromotionRemoved = quote.DiscountAmount <= 0;
            quote.PromotionReasonCode = quote.PromotionRemoved ? "PromotionNoLongerEligible" : null;
        }

        quote.FinalAmount = quote.TotalAmount + quote.WeightSurchargeAmount - quote.DiscountAmount;
        const decimal amountLimit = 10_000_000_000_000_000m; // numeric(18,2) exclusive upper bound
        if (quote.TotalAmount >= amountLimit || quote.FinalAmount >= amountLimit)
            return (null, new("ActualsAmountOutOfRange", "Tổng tiền sau kiểm đếm vượt giới hạn."));
        return (quote, null);
    }
}
