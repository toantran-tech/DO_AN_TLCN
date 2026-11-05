using System.Text.Json.Serialization;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Commands;

public class UpdateCustomerProfileCommand
{
    public string FullName { get; set; } = string.Empty;
    [JsonRequired]
    public string Phone { get; set; } = string.Empty;
    [JsonRequired]
    public string Address { get; set; } = string.Empty;
    [JsonRequired]
    public string City { get; set; } = string.Empty;
    /// <summary>Ảnh đã upload qua /files/images; null = giữ nguyên, chuỗi rỗng = xóa avatar.</summary>
    public string? AvatarUrl { get; set; }
}
