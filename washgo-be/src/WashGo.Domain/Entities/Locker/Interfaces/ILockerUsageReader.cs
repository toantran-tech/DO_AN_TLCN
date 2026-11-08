using System.ComponentModel.DataAnnotations.Schema;

namespace WashGo.Domain.Entities.Locker.Interfaces;

public interface ILockerUsageReader
{
    Task<(List<LockerUsageRecord> Items, int Total)> ReadAsync(Guid lockerId, Guid? merchantId, Guid? boxId, DateTime? from, DateTime? to, int page, int take);
}
public class LockerUsageRecord
{
    [Column("order_id")] public Guid OrderId { get; set; }
    [Column("order_code")] public string OrderCode { get; set; } = string.Empty;
    [Column("box_id")] public Guid? BoxId { get; set; }
    [Column("box_number")] public string BoxNumber { get; set; } = string.Empty;
    [Column("usage_type")] public int UsageType { get; set; }
    [Column("put_in_at")] public DateTime? PutInAt { get; set; }
    [Column("taken_out_at")] public DateTime? TakenOutAt { get; set; }
    [Column("order_status")] public int OrderStatus { get; set; }
}
