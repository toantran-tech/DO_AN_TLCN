using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Domain.Entities.WashOrder
{
    /// <summary>
    /// Một thao tác ghi trên đơn đã thành công, khóa theo operationId (Idempotency-Key) do FE sinh.
    /// Gửi lại cùng operationId trả lại kết quả cũ, không chuyển trạng thái/tạo nhiệm vụ lần nữa.
    /// </summary>
    [SevagoTable(WashGoDbProperties.OrderOperations, Schema = WashGoDbProperties.Schema)]
    public class OrderOperation : Entity<Guid>
    {
        public OrderOperation() { }
        public OrderOperation(Guid id) : base(id) { }

        public Guid OrderId { get; set; }
        /// <summary>operationId của yêu cầu gốc; một thao tác lô (phân công nhiều đơn) có nhiều dòng cùng GroupId.</summary>
        public Guid? GroupId { get; set; }
        public Guid ActorId { get; set; }
        /// <summary>UpdateStatus, UpdateWeight, AssignShipper.</summary>
        public string Operation { get; set; } = string.Empty;
        /// <summary>Tóm tắt nội dung yêu cầu (vd. status=4) để phát hiện dùng lại key cho yêu cầu khác.</summary>
        public string Request { get; set; } = string.Empty;
        public WashOrderStatus ResultStatus { get; set; }
        public long ResultRevision { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? DeliveryTaskId { get; set; }
        /// <summary>Redacted immutable result for a Shipper action; never includes credentials.</summary>
        public string? ResultJson { get; set; }
    }
}
