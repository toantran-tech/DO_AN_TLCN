namespace WashGo.Application.Contracts.Features.PaymentContracts.Results;

public class VnPayIpnResultDto
{
    public string RspCode { get; set; } = "99";
    public string Message { get; set; } = "Unknown error";
}
