using System;
using System.Collections.Generic;

namespace WashGo.Application.Contracts.Features.NotificationContracts.Results
{
    public class ReminderRunResultDto
    {
        public DateTime RanAt { get; set; }
        /// <summary>Số lời nhắc mới đã gửi theo từng loại (khóa nhắc nhở), không tính các lời nhắc đã gửi ở lần quét trước.</summary>
        public Dictionary<string, int> Sent { get; set; } = [];
        public int Total { get; set; }
    }
}
