using System;
using System.Collections.Generic;
using System.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>
    /// Tiến độ thực tế của đơn (UC 1.2.6): các bước theo thứ tự nghiệp vụ (không theo số enum), bước nào áp dụng,
    /// bước nào đã qua (theo lịch sử trạng thái) và phần trăm hoàn thành.
    /// </summary>
    public static class OrderProgressCalculator
    {
        private static readonly (WashOrderStatus Status, string Name)[] Flow =
        [
            (WashOrderStatus.Pending, "Chờ gửi đồ"),
            (WashOrderStatus.Deposited, "Đã gửi đồ"),
            (WashOrderStatus.MerchantAssigned, "Đã phân công tiệm giặt"),
            (WashOrderStatus.CollectionShipperAssigned, "Đã phân công Shipper lấy đồ"),
            (WashOrderStatus.Collected, "Đã lấy đồ về tiệm"),
            (WashOrderStatus.Washing, "Đang giặt"),
            (WashOrderStatus.Drying, "Đang sấy"),
            (WashOrderStatus.Ironing, "Đang ủi"),
            (WashOrderStatus.Folding, "Gấp và đóng gói"),
            (WashOrderStatus.AwaitingPayment, "Chờ thanh toán"),
            (WashOrderStatus.Paid, "Đã thanh toán"),
            (WashOrderStatus.ReadyForReturnDispatch, "Sẵn sàng trả đồ"),
            (WashOrderStatus.ReturnShipperAssigned, "Đã phân công Shipper trả đồ"),
            (WashOrderStatus.ReadyToReturn, "Chờ khách nhận đồ"),
            (WashOrderStatus.TransferringToStorage, "Đang chuyển về tiệm"),
            (WashOrderStatus.MovedToStorage, "Lưu kho tại tiệm"),
            (WashOrderStatus.ReturningFromStorage, "Đang giao lại locker"),
            (WashOrderStatus.Returned, "Khách đã nhận đồ"),
            (WashOrderStatus.Completed, "Hoàn tất")
        ];

        private static readonly WashOrderStatus[] ProcessingSteps =
            [WashOrderStatus.Washing, WashOrderStatus.Drying, WashOrderStatus.Ironing, WashOrderStatus.Folding];

        /// <summary>Mốc có thể đã diễn ra trước khi có trạng thái riêng: suy ra từ mốc sau, không có thời gian giả.</summary>
        private static readonly WashOrderStatus[] InferableSteps =
        [
            WashOrderStatus.MerchantAssigned, WashOrderStatus.CollectionShipperAssigned,
            WashOrderStatus.ReadyForReturnDispatch, WashOrderStatus.ReturnShipperAssigned, WashOrderStatus.Returned
        ];

        /// <summary>
        /// Bước xử lý tại tiệm mà mỗi loại dịch vụ thường đi qua. Đơn mới đi đủ chuỗi Washing → Folding; danh sách này chỉ
        /// còn dùng để hiển thị bước mà đơn cũ đã bỏ qua.
        /// </summary>
        public static IReadOnlyCollection<WashOrderStatus> ProcessingStepsOf(WashServiceType type) => type switch
        {
            WashServiceType.DryClean => [WashOrderStatus.Washing, WashOrderStatus.Ironing, WashOrderStatus.Folding],
            WashServiceType.IronOnly => [WashOrderStatus.Washing, WashOrderStatus.Ironing, WashOrderStatus.Folding],
            WashServiceType.ShoeCare => [WashOrderStatus.Washing, WashOrderStatus.Drying],
            _ => [WashOrderStatus.Washing, WashOrderStatus.Drying, WashOrderStatus.Folding]
        };

        private static int Rank(WashOrderStatus status) => Array.IndexOf(WashOrderStatusFlow.BusinessOrder, status);

        /// <param name="history">Chuyển trạng thái thật (fromStatus khác status).</param>
        /// <param name="evidence">
        /// Thời điểm của sự kiện phân công ghi trước khi có trạng thái riêng (MerchantAssigned, ShipperAssigned theo loại
        /// chuyến); chỉ dùng làm timestamp khi bước đó đã qua, không dùng để coi bước là đã qua.
        /// </param>
        public static (int Percent, List<OrderWorkflowStepDto> Steps) Build(WashOrderStatus status,
            IEnumerable<WashServiceType> serviceTypes, IEnumerable<(WashOrderStatus Status, long Timestamp)> history,
            IEnumerable<(WashOrderStatus Status, long Timestamp)>? evidence = null)
        {
            var reached = history.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Min(x => x.Timestamp));
            var proof = (evidence ?? []).GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Min(x => x.Timestamp));
            var types = serviceTypes.Distinct().ToList();
            var processing = (types.Count == 0 ? [WashServiceType.StandardWash] : types)
                .SelectMany(ProcessingStepsOf).ToHashSet();
            var stored = reached.ContainsKey(WashOrderStatus.MovedToStorage) || status == WashOrderStatus.MovedToStorage;
            var cancelled = status == WashOrderStatus.Cancelled;
            // Furthest milestone really reached, in business order.
            var furthest = reached.Keys.Append(status).Where(x => x != WashOrderStatus.Cancelled).Select(Rank).DefaultIfEmpty(-1).Max();

            var steps = new List<OrderWorkflowStepDto>();
            int applicableCount = 0, reachedCount = 0;
            foreach (var (step, name) in Flow)
            {
                var recorded = reached.ContainsKey(step) || step == status;
                var passed = Rank(step) < furthest;
                var returnDispatch = step is WashOrderStatus.ReadyForReturnDispatch or WashOrderStatus.ReturnShipperAssigned;
                // Legacy orders reached later milestones without these statuses; laundry moved to storage unpaid never had them.
                var inferred = !recorded && passed && InferableSteps.Contains(step) && !(returnDispatch && stored);
                var done = recorded || inferred;
                var applicable = step switch
                {
                    // Every new order goes through the whole chain; a step a legacy order skipped keeps the old rule.
                    _ when ProcessingSteps.Contains(step) => done || !passed || processing.Contains(step),
                    WashOrderStatus.TransferringToStorage or WashOrderStatus.ReturningFromStorage => done,
                    WashOrderStatus.MovedToStorage => stored,
                    // Laundry moved to storage unpaid never sits in a return locker.
                    WashOrderStatus.ReadyToReturn => !stored || done,
                    _ when returnDispatch => !stored || done,
                    _ => true
                };
                if (applicable)
                {
                    applicableCount++;
                    if (done) reachedCount++;
                }
                // After a cancellation only the steps already passed stay on the timeline.
                if (cancelled && !done)
                    applicable = false;
                long? at = reached.TryGetValue(step, out var t) ? t : done && proof.TryGetValue(step, out var e) ? e : null;
                steps.Add(new OrderWorkflowStepDto
                {
                    Status = step,
                    Name = name,
                    IsApplicable = applicable,
                    IsCompleted = done && step != status || status == WashOrderStatus.Completed && step == status,
                    IsCurrent = step == status,
                    Timestamp = at,
                    Basis = !done ? null : reached.ContainsKey(step) ? "StatusChange" : at.HasValue ? "AssignmentEvent" : "NotRecorded"
                });
            }
            if (cancelled)
                steps.Add(new OrderWorkflowStepDto
                {
                    Status = WashOrderStatus.Cancelled,
                    Name = "Đã hủy",
                    IsApplicable = true,
                    IsCompleted = true,
                    IsCurrent = true,
                    Timestamp = reached.TryGetValue(WashOrderStatus.Cancelled, out var cancelledAt) ? cancelledAt : null,
                    Basis = reached.ContainsKey(WashOrderStatus.Cancelled) ? "StatusChange" : "NotRecorded"
                });

            // Share of the applicable flow already reached (the current step counts as reached); a cancelled order
            // keeps the share it had when it was cancelled.
            var percent = status == WashOrderStatus.Completed ? 100
                : applicableCount == 0 ? 0 : (int)Math.Round(100.0 * reachedCount / applicableCount);
            return (Math.Clamp(percent, 0, 100), steps);
        }
    }
}
