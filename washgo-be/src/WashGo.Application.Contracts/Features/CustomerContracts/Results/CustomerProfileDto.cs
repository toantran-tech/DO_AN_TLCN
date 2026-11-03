namespace WashGo.Application.Contracts.Features.CustomerContracts.Results;

public class CustomerProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
    public int LoyaltyPoints { get; set; }
    /// <summary>Số đơn không bị hủy, tính từ đơn hàng thực tế.</summary>
    public int TotalOrders { get; set; }
    /// <summary>Tổng tiền các đơn đã thanh toán.</summary>
    public decimal TotalSpent { get; set; }
}
