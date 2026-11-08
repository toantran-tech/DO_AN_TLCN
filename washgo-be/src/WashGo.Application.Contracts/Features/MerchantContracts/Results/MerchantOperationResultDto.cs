using System;

namespace WashGo.Application.Contracts.Features.MerchantContracts.Results;

public class MerchantOperationResultDto
{
    public Guid OperationId { get; set; }
    public Guid MerchantId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public object Result { get; set; } = null!;
}
