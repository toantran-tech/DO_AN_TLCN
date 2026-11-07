using System;
using System.Collections.Generic;
using System.Linq;
using WashGo.Application.Contracts.Features.WashOrderContracts.Results;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Features.WashOrderApplication.Services
{
    /// <summary>
    /// Tiến độ thực tế của đơn (UC 1.2.6): các bước của quy trình, bước nào áp dụng theo loại dịch vụ của đơn,
    /// bước nào đã qua (theo lịch sử trạng thái) và phần trăm hoàn thành.
    /// </summary>
    public static class OrderProgressCalculator
    {
        private static readonly (WashOrderStatus Status, string Name)[] Flow =
        [
            (WashOrderStatus.Pending, "Chờ gửi đồ"),
            (WashOrderStatus.Deposited, "Đã gửi đồ"),
            (WashOrderStatus.Collected, "Đã lấy đồ về tiệm"),
            (WashOrderStatus.Washing, "Đang giặt"),
            (WashOrderStatus.Drying, "Đang sấy"),
            (WashOrderStatus.Ironing, "Đang ủi"),
            (WashOrderStatus.Folding, "Gấp và đóng gói"),
            (WashOrderStatus.AwaitingPayment, "Chờ thanh toán"),
            (WashOrderStatus.Paid, "Đã thanh toán"),
            (WashOrderStatus.ReadyToReturn, "Đồ sẵn sàng tại locker"),
            (WashOrderStatus.MovedToStorage, "Lưu kho"),
            (WashOrderStatus.Completed, "Hoàn tất")
        ];

        private static readonly WashOrderStatus[] ProcessingSteps =
            [WashOrderStatus.Washing, WashOrderStatus.Drying, WashOrderStatus.Ironing, WashOrderStatus.Folding];

        /// <summary>Bước xử lý tại tiệm mà mỗi loại dịch vụ thường đi qua; Merchant vẫn có thể bỏ qua hoặc thêm bước.</summary>
        public static IReadOnlyCollection<WashOrderStatus> ProcessingStepsOf(WashServiceType type) => type switch
        {
            WashServiceType.DryClean => [WashOrderStatus.Washing, WashOrderStatus.Ironing, WashOrderStatus.Folding],
            WashServiceType.IronOnly => [WashOrderStatus.Washing, WashOrderStatus.Ironing, WashOrderStatus.Folding],
            WashServiceType.ShoeCare => [WashOrderStatus.Washing, WashOrderStatus.Drying],
            _ => [WashOrderStatus.Washing, WashOrderStatus.Drying, WashOrderStatus.Folding]
        };

        public static (int Percent, List<OrderWorkflowStepDto> Steps) Build(WashOrderStatus status,
            IEnumerable<WashServiceType> serviceTypes, IEnumerable<(WashOrderStatus Status, long Timestamp)> history)
        {
            var reached = history.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Min(x => x.Timestamp));
            var types = serviceTypes.Distinct().ToList();
            var processing = (types.Count == 0 ? [WashServiceType.StandardWash] : types)
                .SelectMany(ProcessingStepsOf).ToHashSet();
            var stored = reached.ContainsKey(WashOrderStatus.MovedToStorage) || status == WashOrderStatus.MovedToStorage;
            var cancelled = status == WashOrderStatus.Cancelled;

            var steps = new List<OrderWorkflowStepDto>();
            int applicableCount = 0, reachedCount = 0;
            foreach (var (step, name) in Flow)
            {
                var done = reached.ContainsKey(step) || step == status;
                var applicable = step switch
                {
                    _ when ProcessingSteps.Contains(step) => processing.Contains(step) || done,
                    WashOrderStatus.MovedToStorage => stored,
                    // Laundry moved to storage unpaid never sits in a return locker.
                    WashOrderStatus.ReadyToReturn => !stored || done,
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
                steps.Add(new OrderWorkflowStepDto
                {
                    Status = step,
                    Name = name,
                    IsApplicable = applicable,
                    IsCompleted = done && step != status || status == WashOrderStatus.Completed && step == status,
                    IsCurrent = step == status,
                    Timestamp = reached.TryGetValue(step, out var at) ? at : null
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
                    Timestamp = reached.TryGetValue(WashOrderStatus.Cancelled, out var cancelledAt) ? cancelledAt : null
                });

            // Share of the applicable flow already reached (the current step counts as reached); a cancelled order
            // keeps the share it had when it was cancelled.
            var percent = status == WashOrderStatus.Completed ? 100
                : applicableCount == 0 ? 0 : (int)Math.Round(100.0 * reachedCount / applicableCount);
            return (Math.Clamp(percent, 0, 100), steps);
        }
    }
}
