namespace WashGo.Application.Contracts.Features.CustomerContracts.Commands;

public class UpdateCustomerProfileCommand
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? District { get; set; }
    public string? City { get; set; }
}
