using System;
using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.WashOrderContracts.Results
{
    /// <summary>Kết quả một thao tác ghi đã thực hiện, tra theo operationId sau khi mạng lỗi/timeout.</summary>
    public class OrderOperationDto
    {
        public Guid OperationId { get; set; }
        public Guid OrderId { get; set; }
        /// <summary>UpdateStatus, UpdateWeight, AssignShipper.</summary>
        public string Operation { get; set; } = string.Empty;
        public string Request { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public WashOrderStatus ResultStatus { get; set; }
        public long ResultRevision { get; set; }
        public WashOrderStatus CurrentStatus { get; set; }
        public long CurrentRevision { get; set; }
    }

    /// <summary>meta của response khi thao tác được trả lại (Replayed) hoặc bị từ chối 409.</summary>
    public class OrderWriteMetaDto
    {
        /// <summary>Replayed, OperationIdReused, RevisionMismatch, StatusMismatch.</summary>
        public string Code { get; set; } = string.Empty;
        public bool Replayed { get; set; }
        public Guid? OperationId { get; set; }
        /// <summary>Phiên bản đơn ngay sau lần thực hiện đầu (khi Replayed).</summary>
        public long? ResultRevision { get; set; }
        public long CurrentRevision { get; set; }
        public WashOrderStatus CurrentStatus { get; set; }
    }
}
