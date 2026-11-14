using WashGo.Domain.Shared.Enums;

namespace WashGo.Application.Contracts.Features.CustomerContracts.Results;

public class CustomerLockerSlotStatusDto : CustomerLockerSlotDto
{
    public LockerBoxStatus Status { get; set; }
}
