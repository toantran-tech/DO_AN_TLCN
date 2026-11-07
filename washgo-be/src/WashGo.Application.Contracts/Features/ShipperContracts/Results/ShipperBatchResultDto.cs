using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.ShipperContracts.Results
{
    /// <summary>
    /// Kết quả khóa/xóa hàng loạt: từng Shipper xử lý trong transaction riêng (thành công từng phần).
    /// Gửi lại cùng danh sách an toàn: Shipper đã khóa/xóa trả AlreadyLocked/AlreadyDeleted (thành công).
    /// </summary>
    public class ShipperBatchResultDto
    {
        public int Requested { get; set; }
        /// <summary>Số ID trùng trong yêu cầu đã bỏ qua.</summary>
        public int DuplicatesIgnored { get; set; }
        public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<ShipperBatchItemDto> Results { get; set; } = [];
    }

    public class ShipperBatchItemDto
    {
        public Guid ShipperId { get; set; }
        public bool Succeeded { get; set; }
        /// <summary>Locked, AlreadyLocked, Deleted, AlreadyDeleted, HasActiveTasks, NotFound, NotShipper.</summary>
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
