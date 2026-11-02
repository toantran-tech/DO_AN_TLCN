using System;
using Volo.Abp.Domain.Entities;
using WashGo.Core.Domain.Attributes;
using WashGo.Domain.Shared.Constants;

namespace WashGo.Domain.Entities.Notification
{
    /// <summary>
    /// Đánh dấu một lời nhắc theo thời gian đã được gửi (khóa duy nhất, ví dụ "deposit:{orderId}"),
    /// để worker chạy định kỳ không gửi lặp, kể cả khi người dùng đã xóa thông báo.
    /// </summary>
    [SevagoTable(WashGoDbProperties.ReminderLogs, Schema = WashGoDbProperties.Schema)]
    public class ReminderLog : Entity<Guid>
    {
        public ReminderLog() { }
        public ReminderLog(Guid id) : base(id) { }

        public string Key { get; set; } = string.Empty;
        public Guid? OrderId { get; set; }
        public DateTime SentAt { get; set; }
    }
}
